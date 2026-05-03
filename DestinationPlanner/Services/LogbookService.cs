using DestinationPlanner.Models;
using DestinationPlanner.Serialization;

namespace DestinationPlanner.Services;

public class LogbookService : ILogbookService
{
    private readonly List<FlightRecord> _flights = [];

    public IReadOnlyList<FlightRecord> Flights => _flights.AsReadOnly();
    public string? CurrentFilePath { get; private set; }
    public event EventHandler? FlightsChanged;

    public void AddFlight(FlightRecord flight)
    {
        if (IsDuplicate(flight)) return;
        _flights.Add(flight);
        AutoSave();
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Load(string filePath)
    {
        _flights.Clear();
        _flights.AddRange(NativeLogbookSerializer.Load(filePath));
        CurrentFilePath = filePath;
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void LoadInto(string sourceFilePath, string destFilePath)
    {
        _flights.Clear();
        _flights.AddRange(NativeLogbookSerializer.Load(sourceFilePath));
        CurrentFilePath = destFilePath;
        NativeLogbookSerializer.Save(_flights, destFilePath);
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Export(string filePath)
        => NativeLogbookSerializer.Save(_flights, filePath);

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
        if (added > 0)
        {
            AutoSave();
            FlightsChanged?.Invoke(this, EventArgs.Empty);
        }
        return added;
    }

    private void AutoSave()
    {
        if (CurrentFilePath != null)
            NativeLogbookSerializer.Save(_flights, CurrentFilePath);
    }

    private bool IsDuplicate(FlightRecord candidate)
        => _flights.Any(f =>
            f.DepartureIcao == candidate.DepartureIcao &&
            f.ArrivalIcao == candidate.ArrivalIcao &&
            f.BlockOffUtc == candidate.BlockOffUtc);
}
