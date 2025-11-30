using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexAutomator.Blocks.Base;
using FlexAutomator.Blocks.Triggers;
using FlexAutomator.Blocks.Actions;
using FlexAutomator.Models;
using FlexAutomator.Services;
using FlexAutomator.Services.API;

namespace FlexAutomator.ViewModels;

public partial class ScenarioEditorViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly ScenarioExecutor _executor;
    private readonly TelegramBotService _telegramBotService;
    private Scenario? _currentScenario;

    private AppSettings? _globalSettings;

    [ObservableProperty]
    private string _scenarioName = "Новий сценарій";

    [ObservableProperty]
    private ObservableCollection<BlockViewModel> _blocks = new();

    private readonly List<string> _allTriggerTypes = new()
    {
        "TimeTrigger",
        "CyclicTrigger",
        "HotkeyTrigger",
        "FileChangeTrigger",
        "TelegramCommandTrigger"
    };

    private readonly List<string> _allActionTypes = new()
    {
        "MouseClick",
        "KeyboardInput",
        "ProcessAction",
        "Delay",
        "YouTubeCheck",
        "TMDbCheck",
        "TelegramSend"
    };

    [ObservableProperty]
    private ObservableCollection<string> _availableBlockTypes = new();

    [ObservableProperty]
    private string? _selectedBlockType;

    public ScenarioEditorViewModel(
        DatabaseService databaseService,
        ScenarioExecutor executor,
        TelegramBotService telegramBotService)
    {
        _databaseService = databaseService;
        _executor = executor;
        _telegramBotService = telegramBotService;
    }

    public void Initialize(Scenario? scenario)
    {
        _currentScenario = scenario;
        Blocks.Clear();

        LoadGlobalSettings();

        if (scenario != null)
        {
            ScenarioName = scenario.Name;
            if (!string.IsNullOrEmpty(scenario.BlocksJson))
            {
                var rawBlocks = _executor.DeserializeBlocks(scenario.BlocksJson);
                foreach (var block in rawBlocks)
                {
                    AddBlockViewModel(CreateViewModel(block));
                }
            }
        }
        else
        {
            ScenarioName = "Новий сценарій";
        }

        UpdateAvailableBlocks();
        UpdateBlockStates();
    }

    private async void LoadGlobalSettings()
    {
        try
        {
            _globalSettings = await _databaseService.GetSettingsAsync();
        }
        catch
        {
            _globalSettings = null;
        }
    }

    private void AddBlockViewModel(BlockViewModel? vm)
    {
        if (vm == null) return;

        vm.RequestDelete += OnBlockDelete;
        vm.RequestMoveUp += OnBlockMoveUp;
        vm.RequestMoveDown += OnBlockMoveDown;

        Blocks.Add(vm);
    }

    [RelayCommand]
    private void AddBlock()
    {
        if (string.IsNullOrEmpty(SelectedBlockType)) return;

        BlockViewModel? newVm = SelectedBlockType switch
        {
            "TimeTrigger" => new TimeTriggerViewModel(),
            "CyclicTrigger" => new CyclicTriggerViewModel(),
            "HotkeyTrigger" => new HotkeyTriggerViewModel(),
            "FileChangeTrigger" => new FileChangeTriggerViewModel(),
            "TelegramCommandTrigger" => new TelegramCommandTriggerViewModel(),

            "MouseClick" => new MouseClickActionViewModel(),
            "KeyboardInput" => new KeyboardInputActionViewModel(),
            "ProcessAction" => new ProcessActionViewModel(),
            "Delay" => new DelayActionViewModel(),
            "YouTubeCheck" => new YouTubeCheckViewModel(),
            "TMDbCheck" => new TMDbCheckViewModel(),
            "TelegramSend" => new TelegramSendViewModel(),
            _ => null
        };

        if (newVm != null)
        {
            if (_globalSettings != null)
            {
                if (newVm is YouTubeCheckViewModel ytVm &&
                    !string.IsNullOrWhiteSpace(_globalSettings.YouTubeApiKey))
                {
                    ytVm.YtApiKey = _globalSettings.YouTubeApiKey;
                }
                else if (newVm is TMDbCheckViewModel tmdbVm &&
                         !string.IsNullOrWhiteSpace(_globalSettings.TMDbApiKey))
                {
                    tmdbVm.TmdbKey = _globalSettings.TMDbApiKey;
                }
            }

            AddBlockViewModel(newVm);
            UpdateAvailableBlocks();
            UpdateBlockStates();
            SelectedBlockType = AvailableBlockTypes.FirstOrDefault();
        }
    }

    private void OnBlockDelete(BlockViewModel vm)
    {
        vm.RequestDelete -= OnBlockDelete;
        vm.RequestMoveUp -= OnBlockMoveUp;
        vm.RequestMoveDown -= OnBlockMoveDown;

        if (vm.IsTrigger)
        {
            foreach (var block in Blocks)
            {
                block.RequestDelete -= OnBlockDelete;
                block.RequestMoveUp -= OnBlockMoveUp;
                block.RequestMoveDown -= OnBlockMoveDown;
            }
            Blocks.Clear();
        }
        else
        {
            Blocks.Remove(vm);
        }

        UpdateAvailableBlocks();
        UpdateBlockStates();
    }

    private void OnBlockMoveUp(BlockViewModel vm)
    {
        var index = Blocks.IndexOf(vm);

        if (index > 1)
        {
            Blocks.Move(index, index - 1);
            UpdateBlockStates();
        }
    }

    private void OnBlockMoveDown(BlockViewModel vm)
    {
        var index = Blocks.IndexOf(vm);

        if (index > 0 && index < Blocks.Count - 1)
        {
            Blocks.Move(index, index + 1);
            UpdateBlockStates();
        }
    }

    private void UpdateAvailableBlocks()
    {
        AvailableBlockTypes.Clear();


        bool isTelegramReady = _telegramBotService.IsConfigured && _telegramBotService.IsPaired;

        if (Blocks.Count == 0)
        {
            foreach (var t in _allTriggerTypes)
            {
                if (!isTelegramReady && t == "TelegramCommandTrigger") continue;
                AvailableBlockTypes.Add(t);
            }
        }
        else
        {
            foreach (var a in _allActionTypes)
            {
                if (!isTelegramReady && a == "TelegramSend") continue;
                AvailableBlockTypes.Add(a);
            }
        }
        SelectedBlockType = AvailableBlockTypes.FirstOrDefault();
    }


    private void UpdateBlockStates()
    {
        for (int i = 0; i < Blocks.Count; i++)
        {
            var block = Blocks[i];

            if (block.IsTrigger)
            {
                block.CanMoveUp = false;
                block.CanMoveDown = false;
            }
            else
            {

                block.CanMoveUp = (i > 1);

                block.CanMoveDown = (i < Blocks.Count - 1);
            }
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (_currentScenario == null)
        {
            _currentScenario = new Scenario
            {
                Name = ScenarioName,
                Color = GetRandomColor()
            };
        }
        else
        {
            _currentScenario.Name = ScenarioName;
        }

        var blocksList = Blocks.Select(vm => vm.ToBlock()).ToList();
        _currentScenario.BlocksJson = _executor.SerializeBlocks(blocksList);

        if (_currentScenario.Id == Guid.Empty || !await ScenarioExists(_currentScenario.Id))
        {
            await _databaseService.AddScenarioAsync(_currentScenario);
        }
        else
        {
            await _databaseService.UpdateScenarioAsync(_currentScenario);
        }
    }

    private BlockViewModel? CreateViewModel(Block block) => block switch
    {
        TimeTriggerBlock b => new TimeTriggerViewModel(b),
        CyclicTriggerBlock b => new CyclicTriggerViewModel(b),
        HotkeyTriggerBlock b => new HotkeyTriggerViewModel(b),
        FileChangeTriggerBlock b => new FileChangeTriggerViewModel(b),
        TelegramCommandTriggerBlock b => new TelegramCommandTriggerViewModel(b),

        MouseClickActionBlock b => new MouseClickActionViewModel(b),
        KeyboardInputActionBlock b => new KeyboardInputActionViewModel(b),
        ProcessActionBlock b => new ProcessActionViewModel(b),
        DelayActionBlock b => new DelayActionViewModel(b),
        YouTubeCheckActionBlock b => new YouTubeCheckViewModel(b),
        TMDbCheckActionBlock b => new TMDbCheckViewModel(b),
        TelegramSendActionBlock b => new TelegramSendViewModel(b),

        _ => null
    };

    private async Task<bool> ScenarioExists(Guid id)
    {
        var scenario = await _databaseService.GetScenarioAsync(id);
        return scenario != null;
    }

    private string GetRandomColor()
    {
        var colors = new[] { "#673AB7", "#3F51B5", "#2196F3", "#009688", "#4CAF50", "#FFC107", "#FF5722" };
        return colors[new Random().Next(colors.Length)];
    }
}