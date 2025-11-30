using System.Diagnostics;
using System.IO;
using FlexAutomator.Blocks.Base;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class ProcessActionBlock : ActionBlock
{
    public override string Type => "ProcessAction";

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        await Task.CompletedTask;

        try
        {
            if (!Parameters.TryGetValue("Action", out var action) || string.IsNullOrEmpty(action))
                return BlockResult.Failed("Дія не вказана");

            string? processPath = null;
            string? processName = null;

            Parameters.TryGetValue("Path", out processPath);
            Parameters.TryGetValue("ProcessName", out processName);

            if (string.IsNullOrEmpty(processPath) && string.IsNullOrEmpty(processName))
                return BlockResult.Failed("Шлях або назва процесу не вказані");

            switch (action.ToLower())
            {
                case "open":
                    if (string.IsNullOrEmpty(processPath))
                        return BlockResult.Failed("Для відкриття потрібен шлях до файлу");

                    if (!File.Exists(processPath))
                        return BlockResult.Failed($"Файл не знайдено: {processPath}");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = processPath,
                        UseShellExecute = true
                    });
                    return BlockResult.Successful();

                case "close":

                    var nameToKill = !string.IsNullOrEmpty(processName) 
                        ? processName 
                        : Path.GetFileNameWithoutExtension(processPath);
                    
                    var processes = Process.GetProcessesByName(nameToKill);
                    
                    if (processes.Length == 0)
                        return BlockResult.Failed($"Процес {nameToKill} не запущено");

                    foreach (var process in processes)
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit(5000);
                        }
                        catch
                        {
                        }
                    }
                    return BlockResult.Successful();

                default:
                    return BlockResult.Failed($"Невідома дія: {action}");
            }
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка роботи з процесом: {ex.Message}");
        }
    }
}
