using FlexAutomator.Blocks.Base;
using FlexAutomator.Services; 
using FlexAutomator.Services.API;
using Microsoft.Extensions.DependencyInjection;
using ExecutionContext = FlexAutomator.Blocks.Base.ExecutionContext;

namespace FlexAutomator.Blocks.Actions;

public class TMDbCheckActionBlock : ActionBlock
{
    public override string Type => "TMDbCheck";

    public override async Task<BlockResult> ExecuteAsync(ExecutionContext context)
    {
        try
        {
            if (!Parameters.TryGetValue("Genre", out var genre) || string.IsNullOrEmpty(genre))
                return BlockResult.Failed("Жанр не вказано (Parameter: Genre)");

            if (!Parameters.TryGetValue("ApiKey", out var apiKey) || string.IsNullOrEmpty(apiKey))
                return BlockResult.Failed("API ключ не вказано (Parameter: ApiKey)");

            var region = Parameters.TryGetValue("Region", out var reg) && !string.IsNullOrWhiteSpace(reg)
                ? reg
                : "UA";

            var period = Parameters.TryGetValue("Period", out var per) && !string.IsNullOrWhiteSpace(per)
                ? per
                : "Week";

            var tmdbService = context.ServiceProvider.GetRequiredService<TMDbService>();

            var logService = context.ServiceProvider.GetService<LogService>();

            var newMovies = await tmdbService.CheckForNewMoviesAsync(genre, period, region, apiKey);

            if (newMovies != null && newMovies.Count > 0)
            {
                context.SetVariable(Id, newMovies);

                if (logService != null)
                {
                    var moviesListStr = string.Join("\n", newMovies);
                    logService.Info($"[{Type}] TMDb Check '{genre}' ({period}): Знайдено нові фільми:\n{moviesListStr}");
                }

                return BlockResult.Successful(newMovies);
            }

            return BlockResult.Successful();
        }
        catch (Exception ex)
        {
            return BlockResult.Failed($"Помилка перевірки TMDb: {ex.Message}");
        }
    }
}