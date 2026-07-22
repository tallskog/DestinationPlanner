using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface IAirportDataService
{
    bool IsLoaded { get; }
    int Count { get; }

    // Loads airports.csv (required), runways.csv and airport-frequencies.csv (both optional) from OurAirports.com data.
    Task LoadAsync(string airportsCsvPath, string? runwaysCsvPath = null, string? frequenciesCsvPath = null);

    Airport? GetByIcao(string icao);
    IReadOnlyList<Airport> GetAll();
    IReadOnlyList<Airport> GetInBounds(double minLat, double maxLat, double minLon, double maxLon);

    // Merges Navigraph airport_type classification into already-loaded airports.
    // ICAOs not present in typesByIcao are left at their current Type (default Unclassified).
    // Must be re-invoked after every LoadAsync call, since LoadAsync rebuilds airports from scratch.
    void ApplyAirportTypes(IReadOnlyDictionary<string, AirportType> typesByIcao);
}
