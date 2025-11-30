using System.Net.Http;
using System.Text.Json;

namespace FlexAutomator.Services.API;

public class YouTubeService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, string> _lastVideoCache = new();

    public YouTubeService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<string?> CheckForNewVideoAsync(string channelId, string apiKey)
    {
        try
        {
            var uploadsPlaylistId = "UU" + channelId.Substring(2);

            var url = $"https://www.googleapis.com/youtube/v3/playlistItems?" +
                      $"part=snippet&playlistId={uploadsPlaylistId}&maxResults=1&key={apiKey}";

            var response = await _httpClient.GetStringAsync(url);
            var json = JsonDocument.Parse(response);

            if (json.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                var snippet = items[0].GetProperty("snippet");
                var videoId = snippet.GetProperty("resourceId")
                    .GetProperty("videoId")
                    .GetString();

                if (string.IsNullOrEmpty(videoId))
                    return null;

                var videoUrl = $"https://youtube.com/watch?v={videoId}";

                if (_lastVideoCache.TryGetValue(channelId, out var lastVideoId))
                {
                    if (lastVideoId != videoId)
                    {
                        _lastVideoCache[channelId] = videoId;
                        return videoUrl;
                    }
                }
                else
                {
                    _lastVideoCache[channelId] = videoId;
                    return videoUrl;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}