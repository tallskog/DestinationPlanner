namespace DestinationPlanner.Models;

public class FlightRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string AircraftModel { get; set; } = string.Empty;
    public string DepartureIcao { get; set; } = string.Empty;
    public string ArrivalIcao { get; set; } = string.Empty;
    public DateTime BlockOffUtc { get; set; }
    public DateTime BlockOnUtc { get; set; }

    public TimeSpan BlockTime => BlockOnUtc - BlockOffUtc;
}
