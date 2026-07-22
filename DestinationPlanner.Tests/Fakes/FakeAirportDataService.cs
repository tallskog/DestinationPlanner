using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Fakes;

// Minimal in-memory stand-in for AirportDataService, seeded directly with Airport
// objects so ViewModel tests don't need real CSV files.
public class FakeAirportDataService : IAirportDataService
{
    private readonly Dictionary<string, Airport> _byIcao;

    public FakeAirportDataService(IEnumerable<Airport> airports)
    {
        _byIcao = airports.ToDictionary(a => a.Icao, StringComparer.OrdinalIgnoreCase);
        IsLoaded = true;
    }

    public bool IsLoaded { get; set; }
    public int Count => _byIcao.Count;

    public Task LoadAsync(string airportsCsvPath, string? runwaysCsvPath = null, string? frequenciesCsvPath = null)
        => Task.CompletedTask;

    public Airport? GetByIcao(string icao) => _byIcao.TryGetValue(icao, out var a) ? a : null;

    public IReadOnlyList<Airport> GetAll() => _byIcao.Values.ToList();

    public IReadOnlyList<Airport> GetInBounds(double minLat, double maxLat, double minLon, double maxLon) =>
        _byIcao.Values.Where(a => a.Latitude >= minLat && a.Latitude <= maxLat
                                && a.Longitude >= minLon && a.Longitude <= maxLon).ToList();

    public void ApplyAirportTypes(IReadOnlyDictionary<string, AirportType> typesByIcao)
    {
        foreach (var (icao, type) in typesByIcao)
            if (_byIcao.TryGetValue(icao, out var airport))
                airport.Type = type;
    }
}
