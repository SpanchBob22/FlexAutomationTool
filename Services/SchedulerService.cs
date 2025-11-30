using System.Collections.Concurrent;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Hosting;
using CommunityToolkit.Mvvm.Messaging;
using FlexAutomator.Models;
using FlexAutomator.Blocks.Base;
using FlexAutomator.Blocks.Triggers;
using FlexAutomator.Services.API;
using Telegram.Bot.Types;

namespace FlexAutomator.Services;

public class SchedulerService : IHostedService
{
    private readonly DatabaseService _databaseService;
    private readonly ScenarioExecutor _executor;
    private readonly LogService _logService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly TelegramBotService _telegramBotService;

    private CancellationTokenSource? _cts;
    private readonly List<Task> _scenarioTasks = new();
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private readonly List<TriggerBlock> _loopTriggers = new();

    private List<Scenario> _loadedScenarios = new();

    private readonly Dictionary<string, List<Guid>> _telegramRegistry = new();

    public SchedulerService(
        DatabaseService databaseService,
        ScenarioExecutor executor,
        LogService logService,
        GlobalHotkeyService hotkeyService,
        TelegramBotService telegramBotService)
    {
        _databaseService = databaseService;
        _executor = executor;
        _logService = logService;
        _hotkeyService = hotkeyService;
        _telegramBotService = telegramBotService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logService.Info("Scheduler запускається...");

        try
        {
            var settings = await _databaseService.GetSettingsAsync();

            if (settings != null &&
                !string.IsNullOrEmpty(settings.TelegramBotToken) &&
                settings.IsTelegramBotEnabled)
            {
                _telegramBotService.Initialize(settings.TelegramBotToken, settings.TelegramChatId);

                if (_telegramBotService.IsConfigured)
                {
                    _logService.Info("Автоматичний запуск бота Telegram (увімкнено в налаштуваннях).");
                    await _telegramBotService.StartPollingAsync();
                }
            }
            else
            {
                _logService.Info("Бот Telegram вимкнено або не налаштовано.");
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Не вдалося автоматично підключитися до Telegram: {ex.Message}");
        }

        _hotkeyService.OnScenarioTriggered += ExecuteScenarioById;
        _telegramBotService.OnCommandReceived += OnTelegramCommandReceived;

        await ReloadScenariosAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logService.Info("Зупинка Scheduler...");

        _hotkeyService.OnScenarioTriggered -= ExecuteScenarioById;
        _telegramBotService.OnCommandReceived -= OnTelegramCommandReceived;

        _telegramBotService.StopPolling();
        await CleanupResourcesAsync();
    }

    private void OnTelegramCommandReceived(string command, long chatId)
    {
        var cleanCmd = command.Trim().Replace("/", "").ToLower();

        List<Guid>? scenariosToRun = null;

        lock (_telegramRegistry)
        {
            if (_telegramRegistry.TryGetValue(cleanCmd, out var ids))
            {
                scenariosToRun = new List<Guid>(ids);
            }
        }

        if (scenariosToRun != null && scenariosToRun.Count > 0)
        {
            _logService.Info($"Тригер Telegram спрацював для команди: /{cleanCmd}");
            foreach (var id in scenariosToRun)
            {
                ExecuteScenarioById(id);
            }
        }
        else
        {
            _logService.Info($"Отримано невідому команду: /{cleanCmd}");
        }
    }

    private async Task CleanupResourcesAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            _cts?.Cancel();

            foreach (var trigger in _loopTriggers)
            {
                if (trigger is FileChangeTriggerBlock fileTrigger)
                {
                    fileTrigger.StopWatching();
                }
            }
            _loopTriggers.Clear();

            lock (_telegramRegistry)
            {
                _telegramRegistry.Clear();
            }

            _hotkeyService.ClearAll();

            if (_scenarioTasks.Count > 0)
            {
                await Task.WhenAny(Task.WhenAll(_scenarioTasks), Task.Delay(1000));
            }
            _scenarioTasks.Clear();
        }
        catch (Exception ex)
        {
            _logService.Error($"Помилка при очищенні ресурсів: {ex.Message}");
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task ReloadScenariosAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            _cts?.Cancel();
            foreach (var trigger in _loopTriggers)
            {
                if (trigger is FileChangeTriggerBlock ft) ft.StopWatching();
            }
            _loopTriggers.Clear();
            _scenarioTasks.Clear();
            _hotkeyService.ClearAll();

            lock (_telegramRegistry)
            {
                _telegramRegistry.Clear();
            }

            await Task.Delay(200);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var scenarios = await _databaseService.GetAllScenariosAsync();
            _loadedScenarios = scenarios.Where(s => s.IsActive).ToList();

            _logService.Info($"Завантажено {_loadedScenarios.Count} активних сценаріїв");

            var telegramCommands = new List<BotCommand>();

            foreach (var scenario in _loadedScenarios)
            {
                try
                {
                    var blocks = _executor.DeserializeBlocks(scenario.BlocksJson);
                    if (blocks.Count == 0) continue;

                    var triggerBlock = blocks[0] as TriggerBlock;
                    if (triggerBlock == null) continue;

                    if (triggerBlock is HotkeyTriggerBlock hotkeyBlock)
                    {
                        if (hotkeyBlock.Parameters.TryGetValue("Key", out var keyStr) &&
                            Enum.TryParse<System.Windows.Input.Key>(keyStr, out var key))
                        {
                            var modifiers = hotkeyBlock.Parameters.ContainsKey("Modifiers")
                                ? hotkeyBlock.Parameters["Modifiers"]
                                : "";

                            _hotkeyService.Register(scenario.Id, key, modifiers);
                        }
                    }
                    else if (triggerBlock is TelegramCommandTriggerBlock telegramBlock)
                    {
                        if (telegramBlock.Parameters.TryGetValue("Command", out var rawCmd) && !string.IsNullOrWhiteSpace(rawCmd))
                        {
                            var cleanCmd = rawCmd.Trim().Replace("/", "").ToLower();

                            if (!string.IsNullOrWhiteSpace(cleanCmd))
                            {
                                lock (_telegramRegistry)
                                {
                                    if (!_telegramRegistry.ContainsKey(cleanCmd))
                                    {
                                        _telegramRegistry[cleanCmd] = new List<Guid>();
                                    }
                                    _telegramRegistry[cleanCmd].Add(scenario.Id);
                                }


                                var desc = telegramBlock.Parameters.TryGetValue("Description", out var d) && !string.IsNullOrWhiteSpace(d)
                                    ? d
                                    : scenario.Name;


                                if (desc.Length > 256) desc = desc.Substring(0, 253) + "...";

                                telegramCommands.Add(new BotCommand { Command = cleanCmd, Description = desc });
                            }
                        }
                    }
                    else
                    {
                        _loopTriggers.Add(triggerBlock);

                        if (triggerBlock is FileChangeTriggerBlock fileTrigger)
                        {
                            fileTrigger.StartWatching();
                        }

                        var task = RunScenarioLoopAsync(scenario, triggerBlock, token);
                        _scenarioTasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error($"Помилка ініціалізації сценарія '{scenario.Name}': {ex.Message}");
                }
            }

            if (_telegramBotService.IsConfigured)
            {
                var uniqueCommands = telegramCommands
                    .GroupBy(c => c.Command)
                    .Select(g => g.First())
                    .ToList();

                await _telegramBotService.SetCommandsAsync(uniqueCommands);
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private void ExecuteScenarioById(Guid scenarioId)
    {
        Task.Run(async () =>
        {
            var scenario = _loadedScenarios.FirstOrDefault(s => s.Id == scenarioId);
            if (scenario == null) return;

            var executed = await _executor.ExecuteScenarioAsync(scenario, forceRun: true);

            if (executed)
            {
                await UpdateLastExecutedAsync(scenario);
            }
        });
    }

    private async Task RunScenarioLoopAsync(Scenario scenario, TriggerBlock persistentTrigger, CancellationToken cancellationToken)
    {
        await Task.Delay(new Random().Next(100, 1000), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var shouldRun = await persistentTrigger.ShouldTriggerAsync(scenario.LastExecuted);

                if (shouldRun)
                {
                    Dictionary<Guid, object>? context = null;

                    if (persistentTrigger is FileChangeTriggerBlock fileTrigger && !string.IsNullOrEmpty(fileTrigger.DetectedFilePath))
                    {
                        context = new Dictionary<Guid, object>
                        {
                            { fileTrigger.Id, fileTrigger.DetectedFilePath }
                        };
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var executed = await _executor.ExecuteScenarioAsync(scenario, forceRun: true, initialContext: context);

                            if (executed)
                            {
                                await UpdateLastExecutedAsync(scenario);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.Error($"Помилка виконання сценарію '{scenario.Name}': {ex.Message}");
                        }
                    });
                }

                await Task.Delay(1000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logService.Error($"Помилка в циклі '{scenario.Name}': {ex.Message}");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task UpdateLastExecutedAsync(Scenario scenario)
    {
        try
        {
            var now = DateTime.Now;
            scenario.LastExecuted = now;

            await _databaseService.UpdateScenarioAsync(scenario);

            WeakReferenceMessenger.Default.Send(new ScenarioExecutedMessage(scenario.Id, now));
        }
        catch (Exception ex)
        {
            _logService.Error($"Не вдалося оновити базу даних:: {ex.Message}");
        }
    }
}