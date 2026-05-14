using DestinationPlanner.Models;
using DestinationPlanner.Serialization;
using System.IO;

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

    public void RemoveFlight(Guid id)
    {
        var flight = _flights.FirstOrDefault(f => f.Id == id);
        if (flight is null) return;
        _flights.Remove(flight);
        AutoSave();
        FlightsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateFlight(FlightRecord updated)
    {
        var idx = _flights.FindIndex(f => f.Id == updated.Id);
        if (idx < 0) return;
        _flights[idx] = updated;
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
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        IEnumerable<FlightRecord> imported = ext switch
        {
            ".xml" => ForeignLogbookImporter.Import(filePath),
            ".csv" => LittleNavmapCsvImporter.Import(filePath),
            _      => throw new NotSupportedException($"Unrecognized file format: {ext}")
        };
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

    // Two records for the same dep→arr pair are considered the same flight when:
    //   (a) their block intervals overlap — handles minute-precision imports where
    //       the stored time is just a few seconds off the internally-recorded time, OR
    //   (b) they share the same date and their durations are within 3 minutes — handles
    //       the case where a backup logbook stores wall-clock times that are offset by
    //       hours from the internally-recorded UTC times (e.g. local time stored as UTC).
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromMinutes(3);

    private bool IsDuplicate(FlightRecord candidate)
        => _flights.Any(f =>
            string.Equals(f.DepartureIcao, candidate.DepartureIcao, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(f.ArrivalIcao,   candidate.ArrivalIcao,   StringComparison.OrdinalIgnoreCase) &&
            (
                // (a) overlapping block intervals
                (f.BlockOffUtc < candidate.BlockOnUtc && candidate.BlockOffUtc < f.BlockOnUtc)
                ||
                // (b) same date, durations within tolerance
                (f.Date == candidate.Date &&
                 (f.BlockTime - candidate.BlockTime).Duration() <= DurationTolerance)
            ));
}
