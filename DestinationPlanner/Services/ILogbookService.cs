using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface ILogbookService
{
    IReadOnlyList<FlightRecord> Flights { get; }
    event EventHandler FlightsChanged;
    void AddFlight(FlightRecord flight);
    void Save(string filePath);
    void Load(string filePath);
    int ImportForeign(string filePath);
}
