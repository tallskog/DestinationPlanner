using DestinationPlanner.Models;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Fetches wind speed/direction at a chosen pressure level, for a batch of points, from
// Open-Meteo's free public forecast API (open-meteo.com). No API key required; global
// coverage (unlike NOAA's US-only winds-aloft product). See requirements.md US39.
public class WindDataService : IWindDataService
{
    private const string ForecastEndpoint = "https://api.open-meteo.com/v1/forecast";

    // Retries cover only the two transient failure modes actually observed against
    // Open-Meteo's free tier (see BUG-09/BUG-11 in requirements.md): a burst 429 or a
    // momentary 503. A couple of short backoffs is enough to ride out either without
    // making the rate limit worse. Anything else (bad response, DNS, timeout, ...) fails
    // immediately — retrying those wouldn't help and would just delay the error status.
    private static readonly IReadOnlyList<TimeSpan> DefaultRetryDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    private readonly HttpClient _http;
    private readonly IReadOnlyList<TimeSpan> _retryDelays;

    // httpClient is an injection seam for tests (a fake HttpMessageHandler) — in
    // production a single instance is created once here and reused for the app's lifetime.
    // retryDelays lets tests skip the real backoff wait; defaults to DefaultRetryDelays.
    public WindDataService(HttpClient? httpClient = null, IReadOnlyList<TimeSpan>? retryDelays = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _retryDelays = retryDelays ?? DefaultRetryDelays;
    }

    public async Task<WindFetchResult> GetWindGridAsync(
        IReadOnlyList<(double Lat, double Lon)> points,
        int pressureHPa,
        CancellationToken cancellationToken = default)
    {
        if (points.Count == 0) return WindFetchResult.Success([]);

        string lats = string.Join(",", points.Select(p => p.Lat.ToString("F4", CultureInfo.InvariantCulture)));
        string lons = string.Join(",", points.Select(p => p.Lon.ToString("F4", CultureInfo.InvariantCulture)));
        string url = $"{ForecastEndpoint}?latitude={lats}&longitude={lons}" +
                     $"&hourly=wind_speed_{pressureHPa}hPa,wind_direction_{pressureHPa}hPa" +
                     "&forecast_days=1&timezone=UTC&wind_speed_unit=kn";

        var lastFailure = WindFetchFailure.NetworkError;
        for (int attempt = 0; attempt <= _retryDelays.Count; attempt++)
        {
            try
            {
                string body = await _http.GetStringAsync(url, cancellationToken);
                return WindFetchResult.Success(ParseGrid(body, pressureHPa, DateTime.UtcNow));
            }
            catch (HttpRequestException ex)
            {
                lastFailure = Classify(ex);
                if (lastFailure is not (WindFetchFailure.RateLimited or WindFetchFailure.ServiceUnavailable))
                    return WindFetchResult.Failed(lastFailure);
            }
            catch
            {
                // Includes cancellation: the caller checks its own CancellationTokenSource
                // afterward and discards the result in that case, so the exact failure
                // reason here is never shown — but it must never escape as an unhandled
                // exception into an `async void` event handler further up the call chain.
                return WindFetchResult.Failed(WindFetchFailure.NetworkError);
            }

            if (attempt < _retryDelays.Count)
            {
                try { await Task.Delay(_retryDelays[attempt], cancellationToken); }
                catch { return WindFetchResult.Failed(WindFetchFailure.NetworkError); }
            }
        }

        return WindFetchResult.Failed(lastFailure);
    }

    private static WindFetchFailure Classify(HttpRequestException ex) => ex.StatusCode switch
    {
        HttpStatusCode.TooManyRequests => WindFetchFailure.RateLimited,
        HttpStatusCode.ServiceUnavailable => WindFetchFailure.ServiceUnavailable,
        _ => WindFetchFailure.NetworkError,
    };

    // Internal so tests can exercise parsing directly with canned JSON and a fixed "now".
    internal static IReadOnlyList<WindSample> ParseGrid(string json, int pressureHPa, DateTime nowUtc)
    {
        var result = new List<WindSample>();
        string speedKey = $"wind_speed_{pressureHPa}hPa";
        string dirKey = $"wind_direction_{pressureHPa}hPa";
        string currentHourKey = nowUtc.ToString("yyyy-MM-ddTHH:00", CultureInfo.InvariantCulture);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var location in doc.RootElement.EnumerateArray())
            {
                if (!TryParseLocation(location, speedKey, dirKey, currentHourKey, out var sample))
                    continue;
                result.Add(sample);
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static bool TryParseLocation(
        JsonElement location, string speedKey, string dirKey, string currentHourKey, out WindSample sample)
    {
        sample = default!;

        if (!location.TryGetProperty("latitude", out var latEl) || latEl.ValueKind != JsonValueKind.Number)
            return false;
        if (!location.TryGetProperty("longitude", out var lonEl) || lonEl.ValueKind != JsonValueKind.Number)
            return false;
        if (!location.TryGetProperty("hourly", out var hourly))
            return false;
        if (!hourly.TryGetProperty("time", out var timeEl) || timeEl.ValueKind != JsonValueKind.Array)
            return false;
        if (!hourly.TryGetProperty(speedKey, out var speedArr) || speedArr.ValueKind != JsonValueKind.Array)
            return false;
        if (!hourly.TryGetProperty(dirKey, out var dirArr) || dirArr.ValueKind != JsonValueKind.Array)
            return false;

        int index = -1;
        int i = 0;
        foreach (var t in timeEl.EnumerateArray())
        {
            if (t.ValueKind == JsonValueKind.String && t.GetString() == currentHourKey) { index = i; break; }
            i++;
        }
        // Fall back to the last available hour if the exact current hour isn't present
        // (e.g. clock skew at a day boundary) rather than dropping the point entirely.
        if (index < 0) index = timeEl.GetArrayLength() - 1;
        if (index < 0) return false;

        var speeds = speedArr.EnumerateArray().ToList();
        var dirs = dirArr.EnumerateArray().ToList();
        if (index >= speeds.Count || index >= dirs.Count) return false;
        if (speeds[index].ValueKind != JsonValueKind.Number || dirs[index].ValueKind != JsonValueKind.Number)
            return false;

        sample = new WindSample(
            latEl.GetDouble(), lonEl.GetDouble(),
            dirs[index].GetDouble(), speeds[index].GetDouble());
        return true;
    }
}
