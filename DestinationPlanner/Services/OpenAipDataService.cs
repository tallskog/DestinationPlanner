using DestinationPlanner.Models;
using System.Net.Http;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Fetches airport type classification from OpenAIP's public API (api.core.openaip.net).
// Data is licensed CC BY-NC 4.0 (Attribution-NonCommercial) — see requirements.md US34;
// the app shows an in-app attribution link to https://www.openaip.net per that license.
public class OpenAipDataService : IOpenAipDataService
{
    private const string AirportsEndpoint = "https://api.core.openaip.net/api/airports";
    private const int PageSize = 1000;

    private readonly HttpClient _http;

    // httpClient is an injection seam for tests (a fake HttpMessageHandler) — in
    // production a single instance is created once here and reused for the app's lifetime.
    public OpenAipDataService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<IReadOnlyDictionary<string, AirportType>> FetchAirportTypesAsync(string apiKey, CancellationToken ct)
    {
        var result = new Dictionary<string, AirportType>(StringComparer.OrdinalIgnoreCase);

        int page = 1;
        int totalPages = 1;

        do
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{AirportsEndpoint}?page={page}&limit={PageSize}&fields=icaoCode,type,private");
            request.Headers.Add("x-openaip-api-key", apiKey);

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            totalPages = root.TryGetProperty("totalPages", out var tp) ? tp.GetInt32() : page;

            if (root.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (!item.TryGetProperty("icaoCode", out var icaoEl) || icaoEl.ValueKind != JsonValueKind.String)
                        continue;
                    string icao = icaoEl.GetString()!.Trim();
                    if (icao.Length == 0) continue;

                    int? type = item.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.Number
                        ? typeEl.GetInt32() : null;
                    bool isPrivate = item.TryGetProperty("private", out var privateEl) && privateEl.ValueKind == JsonValueKind.True;

                    result[icao] = MapType(type, isPrivate);
                }
            }

            page++;
        } while (page <= totalPages);

        return result;
    }

    // Maps OpenAIP's /airports 'type' enum (0-13) and 'private' flag to AirportType.
    // 'private' always wins — a privately-operated field matters most to a GA pilot
    // regardless of its civil/military/heliport type code.
    internal static AirportType MapType(int? type, bool isPrivate)
    {
        if (isPrivate) return AirportType.Private;

        return type switch
        {
            4 or 7 => AirportType.Heliport,                            // Heliport Military, Heliport Civil
            5 => AirportType.Military,                                 // Military Aerodrome
            0 or 2 or 3 or 9 => AirportType.Civil,                      // Airport(civil/mil), Airfield Civil, International Airport, Airfield IFR
            1 or 6 or 8 or 10 or 11 or 12 or 13 => AirportType.Other,   // Glider/Ultralight/Closed/Water/Landing Strip/Agricultural/Altiport
            null => AirportType.Unknown,                                // OpenAIP has the airport but no type on record
            _ => AirportType.Other,                                     // defensive default for a future/unrecognized type code
        };
    }
}
