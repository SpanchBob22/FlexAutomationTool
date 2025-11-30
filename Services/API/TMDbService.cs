using System.Net.Http;
using System.Text.Json;

namespace FlexAutomator.Services.API;

public class TMDbService
{
    private readonly HttpClient _httpClient;

    private readonly Dictionary<string, HashSet<int>> _processedMoviesCache = new();

    private readonly object _cacheLock = new();

    public static readonly Dictionary<string, int> GenreMap = new()
    {
        { "Action", 28 },
        { "Adventure", 12 },
        { "Animation", 16 },
        { "Comedy", 35 },
        { "Crime", 80 },
        { "Documentary", 99 },
        { "Drama", 18 },
        { "Family", 10751 },
        { "Fantasy", 14 },
        { "History", 36 },
        { "Horror", 27 },
        { "Music", 10402 },
        { "Mystery", 9648 },
        { "Romance", 10749 },
        { "Science Fiction", 878 },
        { "TV Movie", 10770 },
        { "Thriller", 53 },
        { "War", 10752 },
        { "Western", 37 }
    };

    public TMDbService()
    {
        _httpClient = new HttpClient();
    }

    public Task<List<string>?> CheckForNewMoviesAsync(string genre, string region, string apiKey)
    {
        return CheckForNewMoviesAsync(genre, "Week", region, apiKey);
    }

    public async Task<List<string>?> CheckForNewMoviesAsync(string genreName, string period, string region, string apiKey)
    {
        try
        {
            if (!GenreMap.TryGetValue(genreName, out int genreId))
            {
                if (!int.TryParse(genreName, out genreId))
                    return null;
            }

            var cacheKey = $"{genreId}_{region}";

            var daysBack = period switch
            {
                "Day" => 1,
                "Week" => 7,
                "Month" => 30,
                _ => 7
            };

            var dateGte = DateTime.Today.AddDays(-daysBack).ToString("yyyy-MM-dd");

            var url = $"https://api.themoviedb.org/3/discover/movie?" +
                      $"api_key={apiKey}" +
                      $"&language=uk-UA" +
                      $"&region={region}" +
                      $"&with_genres={genreId}" +
                      $"&primary_release_date.gte={dateGte}" +
                      $"&sort_by=primary_release_date.desc" +
                      $"&vote_count.gte=5" +
                      $"&page=1";

            var response = await _httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            var formattedMovies = new List<string>();

            if (json.RootElement.TryGetProperty("results", out var results))
            {
                lock (_cacheLock)
                {
                    if (!_processedMoviesCache.ContainsKey(cacheKey))
                    {
                        _processedMoviesCache[cacheKey] = new HashSet<int>();
                    }

                    var sentIds = _processedMoviesCache[cacheKey];

                    foreach (var movie in results.EnumerateArray())
                    {
                        var id = movie.GetProperty("id").GetInt32();

                        if (sentIds.Contains(id))
                            continue;

                        sentIds.Add(id);

                        if (formattedMovies.Count >= 10)
                            continue;

                        var title = movie.GetProperty("title").GetString() ?? "Unknown";

                        var releaseDateStr = "N/A";
                        if (movie.TryGetProperty("release_date", out var rDateProp))
                            releaseDateStr = rDateProp.GetString() ?? "N/A";

                        var rating = 0.0;
                        if (movie.TryGetProperty("vote_average", out var vote))
                            rating = vote.GetDouble();

                        var link = $"https://www.themoviedb.org/movie/{id}";
                        formattedMovies.Add($"{title} ({releaseDateStr}) [⭐{rating:F1}] — {link}");
                    }
                }
            }

            return formattedMovies.Count > 0 ? formattedMovies : null;
        }
        catch
        {
            return null;
        }
    }
}