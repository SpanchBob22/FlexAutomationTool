using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexAutomator.Blocks.Base;
using FlexAutomator.Blocks.Triggers;
using FlexAutomator.Blocks.Actions;
using Application = System.Windows.Application;

using WinForms = System.Windows.Forms;
using System.Diagnostics;
using Telegram.Bot.Types;

namespace FlexAutomator.ViewModels;



public abstract partial class BlockViewModel : ObservableObject
{
    public Guid Id { get; }

    [ObservableProperty]
    private bool _canMoveUp;

    [ObservableProperty]
    private bool _canMoveDown;

    public abstract string BlockType { get; }
    public abstract string DisplayName { get; }
    public abstract bool IsTrigger { get; }

    public event Action<BlockViewModel>? RequestDelete;
    public event Action<BlockViewModel>? RequestMoveUp;
    public event Action<BlockViewModel>? RequestMoveDown;

    protected BlockViewModel(Block block)
    {
        Id = block.Id;
    }

    public abstract Block ToBlock();

    [RelayCommand]
    private void Delete() => RequestDelete?.Invoke(this);

    [RelayCommand]
    private void MoveUp() => RequestMoveUp?.Invoke(this);

    [RelayCommand]
    private void MoveDown() => RequestMoveDown?.Invoke(this);

    [RelayCommand]
    private void CopyId()
    {
        try
        {
            System.Windows.Clipboard.SetText($"{{{Id}}}");
        }
        catch (Exception)
        {
        }
    }
}


public partial class TimeTriggerViewModel : BlockViewModel
{
    public override string BlockType => "TimeTrigger";
    public override string DisplayName => "Запуск за часом";
    public override bool IsTrigger => true;

    [ObservableProperty] private DateTime _triggerTime;

    [ObservableProperty] private bool _monday = true;
    [ObservableProperty] private bool _tuesday = true;
    [ObservableProperty] private bool _wednesday = true;
    [ObservableProperty] private bool _thursday = true;
    [ObservableProperty] private bool _friday = true;
    [ObservableProperty] private bool _saturday;
    [ObservableProperty] private bool _sunday;

    public TimeTriggerViewModel(TimeTriggerBlock? block = null) : base(block ?? new TimeTriggerBlock())
    {
        var timeStr = block?.Parameters.TryGetValue("Time", out var t) == true ? t : "12:00";
        if (DateTime.TryParse(timeStr, out var parsedTime))
        {
            TriggerTime = parsedTime;
        }
        else
        {
            TriggerTime = DateTime.Today.AddHours(12);
        }

        if (block == null) return;
        var days = block.Parameters.TryGetValue("Days", out var d) ? d : "";

        Monday = days.Contains("Monday");
        Tuesday = days.Contains("Tuesday");
        Wednesday = days.Contains("Wednesday");
        Thursday = days.Contains("Thursday");
        Friday = days.Contains("Friday");
        Saturday = days.Contains("Saturday");
        Sunday = days.Contains("Sunday");
    }

    public override Block ToBlock()
    {
        var block = new TimeTriggerBlock { Id = Id };

        block.Parameters["Time"] = TriggerTime.ToString("HH:mm");

        var dayList = new List<string>();
        if (Monday) dayList.Add("Monday");
        if (Tuesday) dayList.Add("Tuesday");
        if (Wednesday) dayList.Add("Wednesday");
        if (Thursday) dayList.Add("Thursday");
        if (Friday) dayList.Add("Friday");
        if (Saturday) dayList.Add("Saturday");
        if (Sunday) dayList.Add("Sunday");
        block.Parameters["Days"] = string.Join(",", dayList);
        return block;
    }
}

public partial class CyclicTriggerViewModel : BlockViewModel
{
    public override string BlockType => "CyclicTrigger";
    public override string DisplayName => "Циклічний запуск";
    public override bool IsTrigger => true;

    [ObservableProperty] private string _intervalVal = "60";
    [ObservableProperty] private string _timeUnit = "Хвилини";

    public List<string> Units { get; } = new() { "Секунди", "Хвилини", "Години", "Дні" };

    public CyclicTriggerViewModel(CyclicTriggerBlock? block = null) : base(block ?? new CyclicTriggerBlock())
    {
        if (block == null) return;
        IntervalVal = block.Parameters.TryGetValue("Interval", out var i) ? i : "60";
        TimeUnit = block.Parameters.TryGetValue("Unit", out var u) ? u : "Хвилини";
    }

    public override Block ToBlock()
    {
        var block = new CyclicTriggerBlock { Id = Id };
        block.Parameters["Interval"] = IntervalVal.Trim();
        block.Parameters["Unit"] = TimeUnit;
        return block;
    }
}

public partial class HotkeyTriggerViewModel : BlockViewModel
{
    public override string BlockType => "HotkeyTrigger";
    public override string DisplayName => "Гаряча клавіша";
    public override bool IsTrigger => true;

    [ObservableProperty] private string _hotKeyVal = "F1";
    [ObservableProperty] private bool _isCtrl;
    [ObservableProperty] private bool _isShift;
    [ObservableProperty] private bool _isAlt;

    [ObservableProperty] private bool _isRecording;

    public HotkeyTriggerViewModel(HotkeyTriggerBlock? block = null) : base(block ?? new HotkeyTriggerBlock())
    {
        if (block == null) return;
        HotKeyVal = block.Parameters.TryGetValue("Key", out var k) ? k : "F1";
        var mods = block.Parameters.TryGetValue("Modifiers", out var m) ? m : "";
        IsCtrl = mods.Contains("Ctrl", StringComparison.OrdinalIgnoreCase);
        IsShift = mods.Contains("Shift", StringComparison.OrdinalIgnoreCase);
        IsAlt = mods.Contains("Alt", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void StartRecording()
    {
        IsRecording = true;
        HotKeyVal = "Натисніть клавішу...";
        IsCtrl = false;
        IsShift = false;
        IsAlt = false;
    }

    public void ApplyKey(Key key, ModifierKeys modifiers)
    {
        if (!IsRecording) return;

        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.System) 
        {
            return;
        }

        HotKeyVal = key.ToString();
        IsCtrl = (modifiers & ModifierKeys.Control) != 0;
        IsShift = (modifiers & ModifierKeys.Shift) != 0;
        IsAlt = (modifiers & ModifierKeys.Alt) != 0;

        IsRecording = false;
    }

    public override Block ToBlock()
    {
        var block = new HotkeyTriggerBlock { Id = Id };
        block.Parameters["Key"] = HotKeyVal.Trim();
        var modList = new List<string>();
        if (IsCtrl) modList.Add("Ctrl");
        if (IsShift) modList.Add("Shift");
        if (IsAlt) modList.Add("Alt");
        block.Parameters["Modifiers"] = string.Join("+", modList);
        return block;
    }
}

public partial class FileChangeTriggerViewModel : BlockViewModel
{
    public override string BlockType => "FileChangeTrigger";
    public override string DisplayName => "Файл/Папка";
    public override bool IsTrigger => true;

    [ObservableProperty] private string _watchedPath = "";
    [ObservableProperty] private string _changeEvent = "Changed";
    [ObservableProperty] private bool _isRecursive;

    public List<string> Events { get; } = new() { "Changed", "Created", "Deleted", "Renamed" };

    public FileChangeTriggerViewModel(FileChangeTriggerBlock? block = null) : base(block ?? new FileChangeTriggerBlock())
    {
        if (block == null) return;
        WatchedPath = block.Parameters.TryGetValue("Path", out var p) ? p : "";
        ChangeEvent = block.Parameters.TryGetValue("Event", out var e) ? e : "Changed";
        IsRecursive = block.Parameters.TryGetValue("Recursive", out var r) && bool.TryParse(r, out var b) && b;
    }

    [RelayCommand]
    private void PickFolder()
    {
        using var dialog = new WinForms.FolderBrowserDialog();
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            WatchedPath = dialog.SelectedPath;
        }
    }

    [RelayCommand]
    private void PickFile()
    {
        using var dialog = new WinForms.OpenFileDialog();
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            WatchedPath = dialog.FileName;
        }
    }

    public override Block ToBlock()
    {
        var block = new FileChangeTriggerBlock { Id = Id };
        block.Parameters["Path"] = WatchedPath.Trim();
        block.Parameters["Event"] = ChangeEvent;
        block.Parameters["Recursive"] = IsRecursive.ToString();
        return block;
    }
}

public partial class TelegramCommandTriggerViewModel : BlockViewModel
{
    public override string BlockType => "TelegramCommandTrigger";
    public override string DisplayName => "Команда Telegram";
    public override bool IsTrigger => true;

    [ObservableProperty] private string _botCommand = "/start";
    [ObservableProperty] private string _commandDescription = "";

    public TelegramCommandTriggerViewModel(TelegramCommandTriggerBlock? block = null) : base(block ?? new TelegramCommandTriggerBlock())
    {
        if (block == null) return;
        BotCommand = block.Parameters.TryGetValue("Command", out var c) ? c : "/start";
        CommandDescription = block.Parameters.TryGetValue("Description", out var d) ? d : "";
    }

    public override Block ToBlock()
    {
        var block = new TelegramCommandTriggerBlock { Id = Id };
        block.Parameters["Command"] = BotCommand.Trim();
        block.Parameters["Description"] = CommandDescription.Trim();
        return block;
    }
}



public partial class MouseClickActionViewModel : BlockViewModel
{
    public override string BlockType => "MouseClick";
    public override string DisplayName => "Клік мишею";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _posX = "0";
    [ObservableProperty] private string _posY = "0";
    [ObservableProperty] private string _mouseBtn = "Left";

    public List<string> Buttons { get; } = new() { "Left", "Right" };

    public MouseClickActionViewModel(MouseClickActionBlock? block = null) : base(block ?? new MouseClickActionBlock())
    {
        if (block == null) return;
        PosX = block.Parameters.TryGetValue("X", out var x) ? x : "0";
        PosY = block.Parameters.TryGetValue("Y", out var y) ? y : "0";
        MouseBtn = block.Parameters.TryGetValue("Button", out var b) ? b : "Left";
    }

    [RelayCommand]
    private async Task PickCoordinates()
    {
        Application.Current.MainWindow!.WindowState = WindowState.Minimized;

        await Task.Delay(3000);

        var point = WinForms.Cursor.Position;

        Application.Current.MainWindow.WindowState = WindowState.Normal;
        Application.Current.MainWindow.Activate();

        PosX = point.X.ToString();
        PosY = point.Y.ToString();
    }

    public override Block ToBlock()
    {
        var block = new MouseClickActionBlock { Id = Id };
        block.Parameters["X"] = PosX.Trim();
        block.Parameters["Y"] = PosY.Trim();
        block.Parameters["Button"] = MouseBtn;
        return block;
    }
}

public partial class KeyboardInputActionViewModel : BlockViewModel
{
    public override string BlockType => "KeyboardInput";
    public override string DisplayName => "Введення тексту";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private string _typingMethod = "Type";
    [ObservableProperty] private string _listFormat = "NewLine";

    public List<string> Methods { get; } = new() { "Type", "Paste" };
    public List<string> Formats { get; } = new() { "NewLine", "Comma", "Space" };

    public KeyboardInputActionViewModel(KeyboardInputActionBlock? block = null) : base(block ?? new KeyboardInputActionBlock())
    {
        if (block == null) return;
        InputText = block.Parameters.TryGetValue("Text", out var t) ? t : "";
        TypingMethod = block.Parameters.TryGetValue("InputMethod", out var m) ? m : "Type";
        ListFormat = block.Parameters.TryGetValue("ListFormat", out var f) ? f : "NewLine";
    }

    public override Block ToBlock()
    {
        var block = new KeyboardInputActionBlock { Id = Id };

        block.Parameters["Text"] = InputText;
        block.Parameters["InputMethod"] = TypingMethod;
        block.Parameters["ListFormat"] = ListFormat;
        return block;
    }
}

public partial class ProcessActionViewModel : BlockViewModel
{
    public override string BlockType => "ProcessAction";
    public override string DisplayName => "Програми / Процеси";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _processMode = "Open";
    [ObservableProperty] private string _executablePath = "";
    [ObservableProperty] private string _targetProcessName = "";

    public List<string> Actions { get; } = new() { "Open", "Close" };

    public ProcessActionViewModel(ProcessActionBlock? block = null) : base(block ?? new ProcessActionBlock())
    {
        if (block == null) return;
        ProcessMode = block.Parameters.TryGetValue("Action", out var a) ? a : "Open";
        ExecutablePath = block.Parameters.TryGetValue("Path", out var p) ? p : "";
        TargetProcessName = block.Parameters.TryGetValue("ProcessName", out var n) ? n : "";
    }

    [RelayCommand]
    private void BrowseFile()
    {
        using var dialog = new WinForms.OpenFileDialog();
        dialog.Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*";
        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
            ExecutablePath = dialog.FileName;
            if (string.IsNullOrEmpty(TargetProcessName))
            {
                TargetProcessName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    public override Block ToBlock()
    {
        var block = new ProcessActionBlock { Id = Id };
        block.Parameters["Action"] = ProcessMode;
        block.Parameters["Path"] = ExecutablePath.Trim();
        block.Parameters["ProcessName"] = TargetProcessName.Trim();
        return block;
    }
}

public partial class DelayActionViewModel : BlockViewModel
{
    public override string BlockType => "Delay";
    public override string DisplayName => "Затримка";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _delayDurationMs = "1000";

    public DelayActionViewModel(DelayActionBlock? block = null) : base(block ?? new DelayActionBlock())
    {
        if (block == null) return;
        DelayDurationMs = block.Parameters.TryGetValue("Milliseconds", out var ms) ? ms : "1000";
    }

    public override Block ToBlock()
    {
        var block = new DelayActionBlock { Id = Id };
        block.Parameters["Milliseconds"] = DelayDurationMs.Trim();
        return block;
    }
}

public partial class YouTubeCheckViewModel : BlockViewModel
{
    public override string BlockType => "YouTubeCheck";
    public override string DisplayName => "Перевірка YouTube";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _ytChannelId = "";
    [ObservableProperty] private string _ytApiKey = "";

    public YouTubeCheckViewModel(YouTubeCheckActionBlock? block = null) : base(block ?? new YouTubeCheckActionBlock())
    {
        if (block == null) return;
        YtChannelId = block.Parameters.TryGetValue("ChannelId", out var c) ? c : "";
        YtApiKey = block.Parameters.TryGetValue("ApiKey", out var k) ? k : "";
    }

    public override Block ToBlock()
    {
        var block = new YouTubeCheckActionBlock { Id = Id };
        block.Parameters["ChannelId"] = YtChannelId.Trim();
        block.Parameters["ApiKey"] = YtApiKey.Trim();
        return block;
    }
}

public partial class TMDbCheckViewModel : BlockViewModel
{
    public override string BlockType => "TMDbCheck";
    public override string DisplayName => "Перевірка Фільмів (TMDb)";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _movieGenre = "Action";
    [ObservableProperty] private string _searchPeriod = "Week";
    [ObservableProperty] private string _tmdbKey = "";
    [ObservableProperty] private string _searchRegion = "UA";

    public List<string> Periods { get; } = new() { "Day", "Week", "Month" };
    public List<string> Genres { get; } = new()
    {
        "Action", "Adventure", "Animation", "Comedy", "Crime",
        "Documentary", "Drama", "Family", "Fantasy", "History",
        "Horror", "Music", "Mystery", "Romance", "Science Fiction",
        "TV Movie", "Thriller", "War", "Western"
    };

    public TMDbCheckViewModel(TMDbCheckActionBlock? block = null) : base(block ?? new TMDbCheckActionBlock())
    {
        if (block == null) return;
        MovieGenre = block.Parameters.TryGetValue("Genre", out var g) ? g : "Action";
        SearchPeriod = block.Parameters.TryGetValue("Period", out var p) ? p : "Week";
        TmdbKey = block.Parameters.TryGetValue("ApiKey", out var k) ? k : "";
        SearchRegion = block.Parameters.TryGetValue("Region", out var r) ? r : "UA";
    }

    public override Block ToBlock()
    {
        var block = new TMDbCheckActionBlock { Id = Id };
        block.Parameters["Genre"] = MovieGenre;
        block.Parameters["Period"] = SearchPeriod;
        block.Parameters["ApiKey"] = TmdbKey.Trim();
        block.Parameters["Region"] = SearchRegion.Trim();
        return block;
    }
}

public partial class TelegramSendViewModel : BlockViewModel
{
    public override string BlockType => "TelegramSend";
    public override string DisplayName => "Відправка в Telegram";
    public override bool IsTrigger => false;

    [ObservableProperty] private string _textMessage = "";
    [ObservableProperty] private string _listFormat = "NewLine";

    public List<string> Formats { get; } = new() { "NewLine", "Comma", "Space" };

    public TelegramSendViewModel(TelegramSendActionBlock? block = null) : base(block ?? new TelegramSendActionBlock())
    {
        if (block == null) return;
        TextMessage = block.Parameters.TryGetValue("Message", out var m) ? m : "";
        ListFormat = block.Parameters.TryGetValue("ListFormat", out var f) ? f : "NewLine";
    }

    public override Block ToBlock()
    {
        var block = new TelegramSendActionBlock { Id = Id };
        block.Parameters["Message"] = TextMessage;
        block.Parameters["ListFormat"] = ListFormat;
        return block;
    }
}