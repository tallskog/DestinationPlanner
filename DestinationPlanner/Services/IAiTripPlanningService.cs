using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// AI-assisted trip planning (US41), Claude only. The AI never selects airports, and never
// judges leg distances either — it only (1) turns free text into filter parameters for the
// deterministic ITripCandidateService, and then either (2a) sequences an already-confirmed
// candidate list into an ordered trip with a narrative (no distance constraint given), or
// (2b) writes a narrative for a route TripRouteBuilder already fixed deterministically (a
// per-leg distance constraint was given — see TripPlanViewModel.ConfirmAsync).
public interface IAiTripPlanningService
{
    Task<TripQueryFilters> ParseQueryAsync(string userQuery, CancellationToken ct = default);

    Task<TripPlan> PlanTripAsync(IReadOnlyList<Airport> candidates, string userQuery, string? startIcao, CancellationToken ct = default);

    Task<TripNarrative> NarrateAsync(IReadOnlyList<TripLeg> legs, string userQuery, CancellationToken ct = default);
}
