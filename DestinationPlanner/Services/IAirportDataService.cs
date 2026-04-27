using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface IAirportDataService
{
    bool IsLoaded { get; }
    int Count { get; }

    // Loads airports.csv (required) and runways.csv (optional) from OurAirports.com data.
    Task LoadAsync(string airportsCsvPath, string? runwaysCsvPath = null);

    Airport? GetByIcao(string icao);
    IReadOnlyList<Airport> GetAll();
    IReadOnlyList<Airport> GetInBounds(double minLat, double maxLat, double minLon, double maxLon);
}
