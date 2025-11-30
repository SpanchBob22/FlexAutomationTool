using System.Windows;
using System.Collections.Specialized;
using FlexAutomator.Services;

namespace FlexAutomator.Views;

public partial class LogWindow : Window
{
    private readonly LogService _logService;

    public LogWindow(LogService logService)
    {
        InitializeComponent();
        _logService = logService;

        _logService.Logs.CollectionChanged += Logs_CollectionChanged;
        UpdateLogText();
    }

    private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateLogText();
    }

    private void UpdateLogText()
    {
        var text = string.Join("\n", _logService.Logs.Select(l => $"[{l.Timestamp:HH:mm:ss}] [{l.Level}] {l.Message}"));
        LogTextBlock.Text = text;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _logService.Clear();
    }
}
