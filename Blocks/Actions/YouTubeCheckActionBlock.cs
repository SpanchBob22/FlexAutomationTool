using FlexAutomator.Blocks.Base;
using FlexAutomator.Services;
using FlexAutomator.Services.API;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class YouTubeCheckActionBlock : ActionBlock
{
    public override string Type => "YouTubeCheck";

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        try
        {
            if (!Parameters.TryGetValue("ChannelId", out var channelId) || string.IsNullOrEmpty(channelId))
                return BlockResult.Failed("ID каналу не вказано");

            if (!Parameters.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrEmpty(apiKey))
                return BlockResult.Failed("API ключ не вказано");

            var youtubeService = context.ServiceProvider.GetRequiredService<YouTubeService>();

            var logService = context.ServiceProvider.GetService<LogService>();

            var newVideoUrl = await youtubeService.CheckForNewVideoAsync(channelId, apiKey);

            if (newVideoUrl != null)
            {
                context.SetVariable(Id, newVideoUrl);
                logService?.Info($"YouTube: Знайдено відео на каналі {channelId}: {newVideoUrl}");

                return BlockResult.Successful(newVideoUrl);
            }

            return BlockResult.Successful();
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка перевірки YouTube: {ex.Message}");
        }
    }
}