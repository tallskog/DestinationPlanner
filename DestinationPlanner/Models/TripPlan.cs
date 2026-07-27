namespace DestinationPlanner.Models;

// A saved AI-assisted trip plan (US41). Persisted globally (tripplans.local.json) — never
// scoped to or referencing a specific logbook, so switching or importing a different logbook
// never affects saved plans.
public class TripPlan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public string Title { get; set; } = string.Empty;
    public string Narrative { get; set; } = string.Empty;
    public List<TripLeg> Legs { get; set; } = [];

    // The free-text query that generated this plan, so the user can recall and reuse/tweak it
    // later. Added after the initial release — missing on older tripplans.local.json entries,
    // which deserialize it to "" (System.Text.Json default), never an exception.
    public string Query { get; set; } = string.Empty;
}

public enum TripLegStatus
{
    Planned,
    Flown,
}

public class TripLeg
{
    public int Order { get; set; }
    public string DepartureIcao { get; set; } = string.Empty;
    public string ArrivalIcao { get; set; } = string.Empty;
    public TripLegStatus Status { get; set; } = TripLegStatus.Planned;

    // Set when a logged flight is matched to this leg. One-way pointer forward
    // (plan -> flight) — never a reference back to which logbook produced the match.
    public Guid? FlownFlightId { get; set; }
}
