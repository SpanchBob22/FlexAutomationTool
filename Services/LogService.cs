using System.Collections.ObjectModel;
using FlexAutomator.Models;

namespace FlexAutomator.Services;

public class LogService
{
    public ObservableCollection<LogEntry> Logs { get; } = new();

    public void Info(string message)
    {
        AddLog(LogLevel.Info, message);
    }

    public void Warning(string message)
    {
        AddLog(LogLevel.Warning, message);
    }

    public void Error(string message)
    {
        AddLog(LogLevel.Error, message);
    }

    private void AddLog(LogLevel level, string message)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Logs.Add(new LogEntry
            {
                Level = level,
                Message = message,
                Timestamp = DateTime.Now
            });

            while (Logs.Count > 1000)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Logs.Clear();
        });
    }
}
