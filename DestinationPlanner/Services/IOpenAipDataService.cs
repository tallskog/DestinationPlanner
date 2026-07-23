using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface IOpenAipDataService
{
    // Paginates OpenAIP's GET /airports endpoint and returns the airport_type
    // classification (see OpenAipDataService.MapType) keyed by uppercase ICAO code.
    Task<IReadOnlyDictionary<string, AirportType>> FetchAirportTypesAsync(string apiKey, CancellationToken ct);
}
