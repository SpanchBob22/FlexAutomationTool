using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TgMessage = Telegram.Bot.Types.Message;

namespace FlexAutomator.Services.API;

public class TelegramBotService
{
    private readonly LogService _logService;
    private TelegramBotClient? _botClient;
    private CancellationTokenSource? _cts;


    private long? _chatId;
    private int _offset;


    private string? _pairingCode;
    private DateTime? _pairingExpiration;
    private const int PairingTimeoutSeconds = 120; 


    private readonly ConcurrentQueue<(string Command, DateTime Time)> _recentCommands = new();
    private List<BotCommand> _menuCommands = new(); 
    private List<BotCommand> _allCommands = new();  

    
    public event Action<string, long>? OnCommandReceived;
    public event Action? OnPairingSuccess;

    
    public bool IsConfigured => _botClient != null;
    public bool IsPaired => _chatId.HasValue;
    public bool IsPairingMode => _pairingCode != null && _pairingExpiration > DateTime.Now;
    public string? PairingCode => IsPairingMode ? _pairingCode : null;

    public TelegramBotService(LogService logService)
    {
        _logService = logService;
    }


    public void Initialize(string botToken, long? chatId)
    {
        if (string.IsNullOrWhiteSpace(botToken)) return;

        try
        {
            _botClient = new TelegramBotClient(botToken);
            _chatId = chatId;

            StopPairing();
            _logService.Info("Telegram-бот ініціалізований.");
        }
        catch (Exception ex)
        {
            _logService.Error($"Не вдалося ініціалізувати Telegram-бота: {ex.Message}");
            _botClient = null;
        }
    }


    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tempClient = new TelegramBotClient(token);
            var me = await tempClient.GetMeAsync();
            return me != null && !string.IsNullOrEmpty(me.Username);
        }
        catch
        {
            return false;
        }
    }


    public string StartPairingMode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        var p1 = new string(Enumerable.Repeat(chars, 3).Select(s => s[random.Next(s.Length)]).ToArray());
        var p2 = new string(Enumerable.Repeat(chars, 3).Select(s => s[random.Next(s.Length)]).ToArray());

        _pairingCode = $"{p1}-{p2}";
        _pairingExpiration = DateTime.Now.AddSeconds(PairingTimeoutSeconds);

        _logService.Info($"Режим спарювання запущено. Код: {_pairingCode}. Діє {PairingTimeoutSeconds}секунд.");
        return _pairingCode;
    }

    public void StopPairing()
    {
        _pairingCode = null;
        _pairingExpiration = null;
    }

    public void Disconnect()
    {
        StopPolling();
        _chatId = null;
        _botClient = null;
        StopPairing();
        _logService.Info("Telegram-бот відключено локально.");
    }


    public async Task SetCommandsAsync(List<BotCommand> commands)
    {
        if (_botClient == null) return;

        _allCommands = commands;


        var validMenuRegex = new Regex("^[a-z0-9_]{1,32}$");

        _menuCommands = commands
            .Where(c => validMenuRegex.IsMatch(c.Command))
            .ToList();

        try
        {
            await _botClient.SetMyCommandsAsync(_menuCommands);
            _logService.Info($"Меню Telegram оновлено. Зареєстровано {_menuCommands.Count} команд із {_allCommands.Count}.");
        }
        catch (Exception ex)
        {
            _logService.Warning($"Не вдалося оновити меню Telegram: {ex.Message}");
        }
    }

    public async Task StartPollingAsync(Action<string>? onMessageCallback = null)
    {
        if (_botClient == null) return;

        StopPolling();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _logService.Info("Опитування Telegram запущено.");

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var updates = await _botClient.GetUpdatesAsync(
                        offset: _offset,
                        timeout: 30, 
                        cancellationToken: token
                    );

                    foreach (var update in updates)
                    {
                        _offset = update.Id + 1;

                        if (update.Type == UpdateType.Message && update.Message?.Text != null)
                        {
                            await HandleMessageAsync(update.Message, onMessageCallback);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {

                    if (!ex.Message.Contains("Скасовано"))
                    {
                        _logService.Warning($"Попередження Telegram-полінгу: {ex.Message}. Повторна спроба...");
                    }
                    await Task.Delay(5000, token);
                }
            }
            _logService.Info("Опитування Telegram зупинено.");
        }, token);
    }

    private async Task HandleMessageAsync(TgMessage message, Action<string>? callback)
    {
        var text = message.Text;
        if (_botClient == null || text == null) return;

        var incomingChatId = message.Chat.Id;


        if (IsPairingMode &&
            string.Equals(text.Trim(), $"/pair {_pairingCode}", StringComparison.OrdinalIgnoreCase))
        {
            _chatId = incomingChatId;
            StopPairing();

            _logService.Info($"Telegram успішно підключено з ChatID: {_chatId}");
            await SendMessageAsync("✅ FlexAutomator успішно підключено! Тепер я приймаю ваші команди.");

            OnPairingSuccess?.Invoke();
            return;
        }


        if (!_chatId.HasValue || incomingChatId != _chatId.Value)
        {
            return;
        }

        if (text.Equals("/commands", StringComparison.OrdinalIgnoreCase))
        {
            await SendCommandListAsync();
            return;
        }

        _logService.Info($"Команда Telegram отримана: {text}");
        OnCommandReceived?.Invoke(text, incomingChatId);
        callback?.Invoke(text);

        _recentCommands.Enqueue((text, DateTime.Now));
        CleanupRecentCommands();
    }

    private async Task SendCommandListAsync()
    {
        if (_botClient == null || !_chatId.HasValue) return;

        var sb = new StringBuilder();
        sb.AppendLine("<b>📋 Усі активні команди:</b>\n");

        if (_allCommands.Count > 0)
        {
            foreach (var cmd in _allCommands)
            {
                var safeCmd = WebUtility.HtmlEncode(cmd.Command);
                var safeDesc = WebUtility.HtmlEncode(cmd.Description);


                var inMenu = _menuCommands.Any(mc => mc.Command == cmd.Command);
                var prefix = inMenu ? "🔹" : "🔸"; 

                sb.AppendLine($"{prefix} <code>/{safeCmd}</code> — {safeDesc}");
            }
            sb.AppendLine("\n<i>🔹 - є в меню | 🔸 - тільки вводом</i>");
        }
        else
        {
            sb.AppendLine("Немає активних сценаріїв з Telegram-тригерами.");
        }

        try
        {
            await _botClient.SendTextMessageAsync(_chatId.Value, sb.ToString(), parseMode: ParseMode.Html);
        }
        catch (Exception ex)
        {
            _logService.Error($"Помилка під час відправлення списку команд:: {ex.Message}");
        }
    }

    public void StopPolling()
    {
        _cts?.Cancel();
    }

    public async Task SendMessageAsync(string message)
    {
        if (_botClient != null && _chatId.HasValue)
        {
            try
            {
                await _botClient.SendTextMessageAsync(_chatId.Value, message);
            }
            catch (Exception ex)
            {
                _logService.Error($"Не вдалося відправити повідомлення у Telegram: {ex.Message}");
            }
        }
    }

    public bool HasCommandSince(string command, DateTime? since)
    {
        var checkTime = since ?? DateTime.Now.AddSeconds(-10);
        return _recentCommands.Any(c => c.Command.Equals(command, StringComparison.OrdinalIgnoreCase) && c.Time > checkTime);
    }

    private void CleanupRecentCommands()
    {
        while (_recentCommands.TryPeek(out var cmd) && (DateTime.Now - cmd.Time).TotalMinutes > 1)
        {
            _recentCommands.TryDequeue(out _);
        }
    }

    public long? GetChatId() => _chatId;
}