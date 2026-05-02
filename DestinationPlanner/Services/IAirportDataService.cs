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
}
