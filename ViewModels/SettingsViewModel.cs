using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexAutomator.Models;
using FlexAutomator.Services;
using FlexAutomator.Services.API;

namespace FlexAutomator.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly TelegramBotService _telegramService;
    private readonly BootConfigService _bootConfigService;

    [ObservableProperty] private string _currentDbPath = "Невідомо";

    [ObservableProperty] private string? _youTubeApiKey;
    [ObservableProperty] private string? _tmDbApiKey;

    [ObservableProperty] private string? _telegramBotToken;
    [ObservableProperty] private bool _isTelegramBotEnabled;

    [ObservableProperty] private string _connectionStatus = "Невідомо";
    [ObservableProperty] private string _connectionColor = "Gray";
    [ObservableProperty] private bool _isPaired;

    [ObservableProperty] private string? _pairingCode;
    [ObservableProperty] private bool _isPairing;
    [ObservableProperty] private string _pairingTimeRemaining = "";
    [ObservableProperty] private double _pairingProgress = 100;

    private CancellationTokenSource? _pairingCts;

    public SettingsViewModel(
        DatabaseService databaseService,
        TelegramBotService telegramService,
        BootConfigService bootConfigService)
    {
        _databaseService = databaseService;
        _telegramService = telegramService;
        _bootConfigService = bootConfigService;

        _telegramService.OnPairingSuccess += OnPairingSuccess;

        LoadSettingsAsync();
    }

    private async void LoadSettingsAsync()
    {
        var bootConfig = _bootConfigService.Load();
        if (bootConfig != null)
        {
            CurrentDbPath = bootConfig.DbPath;
        }

        var settings = await _databaseService.GetSettingsAsync();
        if (settings != null)
        {
            YouTubeApiKey = settings.YouTubeApiKey;
            TmDbApiKey = settings.TMDbApiKey;
            TelegramBotToken = settings.TelegramBotToken;
            IsTelegramBotEnabled = settings.IsTelegramBotEnabled;

            if (_telegramService.IsPaired)
            {
                UpdateStatus(true, "Підключено ✅", "Green");
            }
            else if (!string.IsNullOrEmpty(TelegramBotToken))
            {
                UpdateStatus(false, "Потрібне підключення ⚠️", "Orange");
            }
            else
            {
                UpdateStatus(false, "Не налаштовано ⚪", "Gray");
            }
        }
    }

    private void UpdateStatus(bool isPaired, string text, string color)
    {
        IsPaired = isPaired;
        ConnectionStatus = text;
        ConnectionColor = color;
    }

    private void OnPairingSuccess()
    {
        _pairingCts?.Cancel();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            IsPairing = false;
            PairingCode = null;
            UpdateStatus(true, "Підключено успішно!", "Green");
        });

        Task.Run(async () =>
        {
            var settings = await _databaseService.GetSettingsAsync() ?? new AppSettings();
            settings.TelegramChatId = _telegramService.GetChatId();
            settings.TelegramBotToken = TelegramBotToken;
            await _databaseService.SaveSettingsAsync(settings);
        });
    }

    [RelayCommand]
    private async Task StartPairing()
    {
        if (string.IsNullOrWhiteSpace(TelegramBotToken))
        {
            System.Windows.MessageBox.Show("Спочатку введіть Token бота!", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        IsPairing = true;
        PairingCode = "Перевірка токена...";
        UpdateStatus(false, "Перевірка...", "Blue");

        var isValid = await _telegramService.ValidateTokenAsync(TelegramBotToken);
        if (!isValid)
        {
            IsPairing = false;
            PairingCode = null;
            UpdateStatus(false, "Невірний токен ❌", "Red");
            System.Windows.MessageBox.Show("Токен недійсний. Перевірте правильність введення.", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }

        _telegramService.Initialize(TelegramBotToken, null);
        await _telegramService.StartPollingAsync();

        var code = _telegramService.StartPairingMode();
        PairingCode = code;

        UpdateStatus(false, "Очікування коду... ⏳", "Orange");

        _pairingCts?.Cancel();
        _pairingCts = new CancellationTokenSource();
        _ = RunPairingTimer(_pairingCts.Token);
    }

    private async Task RunPairingTimer(CancellationToken token)
    {
        const int totalSeconds = 120;

        try
        {
            for (int i = totalSeconds; i >= 0; i--)
            {
                if (token.IsCancellationRequested) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    PairingTimeRemaining = $"{i / 60:00}:{i % 60:00}";
                    PairingProgress = (double)i / totalSeconds * 100;
                });

                await Task.Delay(1000, token);
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (IsPairing)
                {
                    IsPairing = false;
                    PairingCode = null;
                    _telegramService.StopPairing();
                    UpdateStatus(false, "Час вичерпано ⏰", "Red");
                }
            });
        }
        catch (TaskCanceledException)
        {
        }
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        if (System.Windows.MessageBox.Show("Ви дійсно хочете відключити бота? Сценарії перестануть працювати.", "Підтвердження",
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
        {
            _telegramService.Disconnect();

            var settings = await _databaseService.GetSettingsAsync();
            if (settings != null)
            {
                settings.TelegramChatId = null;
                await _databaseService.SaveSettingsAsync(settings);
            }

            UpdateStatus(false, "Відключено ⚪", "Gray");
            IsPairing = false;
            PairingCode = null;
        }
    }

    [RelayCommand]
    private void CopyCode()
    {
        if (!string.IsNullOrEmpty(PairingCode))
        {
            try
            {
                System.Windows.Clipboard.SetText($"/pair {PairingCode}");
            }
            catch { }
        }
    }

    [RelayCommand]
    private void ChangeDatabaseLocation()
    {
        var result = System.Windows.MessageBox.Show(
            "Ви дійсно хочете змінити папку бази даних?\n\n" +
            "Поточний шлях буде забуто, і програма перезапуститься, щоб ви могли вибрати нову папку (або створити нову базу).\n\n" +
            "Файли в поточній папці НЕ будуть видалені.",
            "Зміна папки даних",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlexAutomator", "app_config.json");
                if (File.Exists(configPath))
                {
                    File.Delete(configPath);
                }

                var currentProcess = Environment.ProcessPath;
                if (currentProcess != null)
                {
                    Process.Start(currentProcess);
                }

                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Не вдалося скинути налаштування: {ex.Message}", "Помилка", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        var settings = await _databaseService.GetSettingsAsync() ?? new AppSettings();

        settings.YouTubeApiKey = YouTubeApiKey;
        settings.TMDbApiKey = TmDbApiKey;
        settings.TelegramBotToken = TelegramBotToken;
        settings.IsTelegramBotEnabled = IsTelegramBotEnabled;

        await _databaseService.SaveSettingsAsync(settings);

        if (!IsTelegramBotEnabled)
        {
            _telegramService.StopPolling();
        }
    }
}