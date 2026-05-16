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

    // Landing statistics — null for manually entered flights and pre-v1.1 logbook entries.
    public double? LandingFpm           { get; set; }
    public double? LandingGForce        { get; set; }
    public double? LandingAirspeedKts   { get; set; }
    public double? LandingWindKts       { get; set; }
    public double? LandingWindDirection { get; set; }

    public string LandingFpmDisplay =>
        LandingFpm.HasValue ? $"{LandingFpm.Value:+0;-0;0}" : "—";
    public string LandingGDisplay =>
        LandingGForce.HasValue ? $"{LandingGForce.Value:F1}g" : "—";
    public string LandingAirspeedDisplay =>
        LandingAirspeedKts.HasValue ? $"{LandingAirspeedKts.Value:F0}kt" : "—";
    public string LandingWindDisplay =>
        (LandingWindKts.HasValue && LandingWindDirection.HasValue)
            ? $"{LandingWindKts.Value:F0}kt/{LandingWindDirection.Value:F0}°"
            : "—";
}
