using System.Net.Http;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Fetches the current precipitation radar frame (rain, snow, sleet, or hail) from
// RainViewer's public API (rainviewer.com). Free for personal/non-commercial use; the app
// shows an attribution link next to the map's precipitation toggle. No API key required.
// See requirements.md US38.
public class PrecipitationRadarService : IPrecipitationRadarService
{
    private const string WeatherMapsEndpoint = "https://api.rainviewer.com/public/weather-maps.json";
    private const int TileSize = 256;
    private const int ColorScheme = 2;     // RainViewer's "Universal Blue" scheme
    private const string TileOptions = "1_1"; // smooth=1, show snow in a distinct color=1

    private readonly HttpClient _http;

    // httpClient is an injection seam for tests (a fake HttpMessageHandler) — in
    // production a single instance is created once here and reused for the app's lifetime.
    public PrecipitationRadarService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<PrecipitationRadarFrame?> GetLatestFrameAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string body = await _http.GetStringAsync(WeatherMapsEndpoint, cancellationToken);
            return ParseLatestFrame(body);
        }
        catch
        {
            return null;
        }
    }

    // Internal so tests can exercise the parsing logic directly with canned JSON,
    // without needing a fake HttpMessageHandler for every edge case.
    internal static PrecipitationRadarFrame? ParseLatestFrame(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("host", out var hostEl) || hostEl.ValueKind != JsonValueKind.String)
                return null;
            string? host = hostEl.GetString();
            if (string.IsNullOrEmpty(host)) return null;

            if (!root.TryGetProperty("radar", out var radar) ||
                !radar.TryGetProperty("past", out var past) ||
                past.ValueKind != JsonValueKind.Array)
                return null;

            // The API returns frames oldest-first; the last entry is the most recently observed.
            JsonElement? latest = null;
            foreach (var frame in past.EnumerateArray())
                latest = frame;
            if (latest is null) return null;
            var f = latest.Value;

            if (!f.TryGetProperty("path", out var pathEl) || pathEl.ValueKind != JsonValueKind.String)
                return null;
            string? path = pathEl.GetString();
            if (string.IsNullOrEmpty(path)) return null;

            if (!f.TryGetProperty("time", out var timeEl) || timeEl.ValueKind != JsonValueKind.Number)
                return null;
            long unixTime = timeEl.GetInt64();

            string tileUrlTemplate = $"{host}{path}/{TileSize}/{{z}}/{{x}}/{{y}}/{ColorScheme}/{TileOptions}.png";
            return new PrecipitationRadarFrame(tileUrlTemplate, DateTimeOffset.FromUnixTimeSeconds(unixTime));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
