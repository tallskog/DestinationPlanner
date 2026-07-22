using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Fakes;

public class FakeLogbookService : ILogbookService
{
    private List<FlightRecord> _flights = [];

    public IReadOnlyList<FlightRecord> Flights => _flights;
    public string? CurrentFilePath => null;
    public event EventHandler FlightsChanged = delegate { };

    public void SetFlights(IEnumerable<FlightRecord> flights)
    {
        _flights = flights.ToList();
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddFlight(FlightRecord flight)
    {
        _flights.Add(flight);
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveFlight(Guid id) => _flights.RemoveAll(f => f.Id == id);
    public void UpdateFlight(FlightRecord updated) { }
    public void Load(string filePath) { }
    public void LoadInto(string sourceFilePath, string destFilePath) { }
    public void Export(string filePath) { }
    public int ImportForeign(string filePath) => 0;
}
