using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface ILogbookService
{
    IReadOnlyList<FlightRecord> Flights { get; }
    string? CurrentFilePath { get; }
    event EventHandler FlightsChanged;
    void AddFlight(FlightRecord flight);
    /// <summary>Load from filePath and set it as the active logbook (auto-saves on changes).</summary>
    void Load(string filePath);
    /// <summary>Load flights from sourcePath but save to and track destPath (US15.1 import).</summary>
    void LoadInto(string sourceFilePath, string destFilePath);
    /// <summary>Save current flights to an arbitrary path without changing CurrentFilePath (US15.2 export).</summary>
    void Export(string filePath);
    int ImportForeign(string filePath);
}
