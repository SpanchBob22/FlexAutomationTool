using FlexAutomator.Blocks.Base;
using FlexAutomator.Services.API;
using Microsoft.Extensions.DependencyInjection;

namespace FlexAutomator.Blocks.Triggers;

public class TelegramCommandTriggerBlock : TriggerBlock
{
    public override string Type => "TelegramCommandTrigger";

    public override async Task<bool> ShouldTriggerAsync(DateTime? lastExecuted = null)
    {
        await Task.CompletedTask;

        if (!Parameters.TryGetValue("Command", out var command) || string.IsNullOrEmpty(command))
            return false;

        if (App.Current is App app && app.Host != null)
        {
            var telegramService = app.Host.Services.GetRequiredService<TelegramBotService>();
            if (telegramService.IsConfigured)
            {
                return telegramService.HasCommandSince(command, lastExecuted);
            }
        }

        return false;
    }
}
