using FlexAutomator.Blocks.Base;
using FlexAutomator.Services;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class DelayActionBlock : ActionBlock
{
    public override string Type => "Delay";

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        try
        {
            if (!Parameters.TryGetValue("Milliseconds", out var msStr) || string.IsNullOrEmpty(msStr))
                return BlockResult.Failed("Тривалість затримки не вказана");

            if (!int.TryParse(msStr, out var ms) || ms < 0)
                return BlockResult.Failed("Некоректна тривалість затримки");

            var logService = context.ServiceProvider.GetService<LogService>();
            logService?.Info($"[Delay] Початок затримки: {ms}ms");

            await Task.Delay(ms);

            logService?.Info($"[Delay] Затримка завершена: {ms}ms");

            return BlockResult.Successful();
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка затримки: {ex.Message}");
        }
    }
}
