using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FlexAutomator.Models;
using FlexAutomator.Services;
using FlexAutomator.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FlexAutomator.ViewModels;

public partial class MainViewModel : ObservableObject, IRecipient<ScenarioExecutedMessage>
{
    private readonly DatabaseService _databaseService;
    private readonly SchedulerService _schedulerService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ObservableCollection<Scenario> _scenarios = new();

    public MainViewModel(
        DatabaseService databaseService,
        SchedulerService schedulerService,
        IServiceProvider serviceProvider)
    {
        _databaseService = databaseService;
        _schedulerService = schedulerService;
        _serviceProvider = serviceProvider;

        WeakReferenceMessenger.Default.Register(this);

        LoadScenariosAsync();
    }

    public void Receive(ScenarioExecutedMessage message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var scenario = Scenarios.FirstOrDefault(s => s.Id == message.Value);
            if (scenario != null)
            {
                scenario.LastExecuted = message.ExecutedAt;
            }
        });
    }

    private async void LoadScenariosAsync()
    {
        Scenarios.Clear();
        var scenarios = await _databaseService.GetAllScenariosAsync();

        foreach (var scenario in scenarios)
        {
            Scenarios.Add(scenario);
        }
    }

    [RelayCommand]
    private void CreateScenario()
    {
        var editor = _serviceProvider.GetRequiredService<ScenarioEditorWindow>();
        editor.ViewModel.Initialize(null);
        editor.ShowDialog();

        RefreshScenarios();
    }

    [RelayCommand]
    private void EditScenario(Scenario scenario)
    {
        var editor = _serviceProvider.GetRequiredService<ScenarioEditorWindow>();
        editor.ViewModel.Initialize(scenario);
        editor.ShowDialog();

        RefreshScenarios();
    }

    [RelayCommand]
    private async Task DeleteScenario(Scenario scenario)
    {
        Scenarios.Remove(scenario);
        await _databaseService.DeleteScenarioAsync(scenario.Id);
        await _schedulerService.ReloadScenariosAsync();
        RequestHotkeyRefresh?.Invoke();
    }

    [RelayCommand]
    private async Task ToggleActive(Scenario scenario)
    {
        await _databaseService.UpdateScenarioAsync(scenario);
        await _schedulerService.ReloadScenariosAsync();
        RequestHotkeyRefresh?.Invoke();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
        settingsWindow.ShowDialog();
    }

    [RelayCommand]
    private void OpenLogs()
    {
        var logWindow = _serviceProvider.GetRequiredService<LogWindow>();
        logWindow.Show();
    }

    public event Action? RequestHotkeyRefresh;

    private async void RefreshScenarios()
    {
        Scenarios.Clear();
        var scenarios = await _databaseService.GetAllScenariosAsync();

        foreach (var scenario in scenarios)
        {
            Scenarios.Add(scenario);
        }

        await _schedulerService.ReloadScenariosAsync();
        RequestHotkeyRefresh?.Invoke();
    }
}