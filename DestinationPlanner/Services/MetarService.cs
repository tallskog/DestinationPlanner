using System.Net.Http;

namespace DestinationPlanner.Services;

public class MetarService : IMetarService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<string?> FetchMetarAsync(string icao, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://aviationweather.gov/api/data/metar?ids={icao}&format=raw";
            var response = await _http.GetStringAsync(url, cancellationToken);
            var metar = response.Trim();
            return string.IsNullOrEmpty(metar) ? null : metar;
        }
        catch
        {
            return null;
        }
    }
}
