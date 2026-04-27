using DestinationPlanner.Models;
using DestinationPlanner.Serialization;

namespace DestinationPlanner.Services;

public class LogbookService : ILogbookService
{
    private readonly List<FlightRecord> _flights = [];

    public IReadOnlyList<FlightRecord> Flights => _flights.AsReadOnly();
    public event EventHandler? FlightsChanged;

    public void AddFlight(FlightRecord flight)
    {
        if (!IsDuplicate(flight))
        {
            _flights.Add(flight);
            FlightsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Save(string filePath)
        => NativeLogbookSerializer.Save(_flights, filePath);

    public void Load(string filePath)
    {
        _flights.Clear();
        _flights.AddRange(NativeLogbookSerializer.Load(filePath));
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public int ImportForeign(string filePath)
    {
        var imported = ForeignLogbookImporter.Import(filePath);
        int added = 0;
        foreach (var flight in imported)
        {
            if (!IsDuplicate(flight))
            {
                _flights.Add(flight);
                added++;
            }
        }
        if (added > 0) FlightsChanged?.Invoke(this, EventArgs.Empty);
        return added;
    }

    private bool IsDuplicate(FlightRecord candidate)
        => _flights.Any(f =>
            f.DepartureIcao == candidate.DepartureIcao &&
            f.ArrivalIcao == candidate.ArrivalIcao &&
            f.BlockOffUtc == candidate.BlockOffUtc);
}
