using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Fakes;

// Hand-written stand-in for AnthropicTripPlanningService, mirroring FakeAirportDataService's
// convention — no network, no real Claude call, results configured directly by the test.
public class FakeAiTripPlanningService : IAiTripPlanningService
{
    public TripQueryFilters QueryFiltersToReturn { get; set; } = new();
    public Func<IReadOnlyList<Airport>, string, string?, TripPlan>? PlanFactory { get; set; }
    public TripNarrative NarrativeToReturn { get; set; } = new("Test Plan", "Test narrative");
    public int ParseQueryCallCount { get; private set; }
    public int PlanTripCallCount { get; private set; }
    public int NarrateCallCount { get; private set; }

    public Task<TripQueryFilters> ParseQueryAsync(string userQuery, CancellationToken ct = default)
    {
        ParseQueryCallCount++;
        return Task.FromResult(QueryFiltersToReturn);
    }

    public Task<TripPlan> PlanTripAsync(IReadOnlyList<Airport> candidates, string userQuery, string? startIcao, CancellationToken ct = default)
    {
        PlanTripCallCount++;
        var plan = PlanFactory?.Invoke(candidates, userQuery, startIcao) ?? new TripPlan
        {
            Title = "Test Plan",
            Narrative = "Test narrative",
            Legs = candidates.Zip(candidates.Skip(1), (from, to) => (from, to))
                .Select((pair, i) => new TripLeg { Order = i + 1, DepartureIcao = pair.from.Icao, ArrivalIcao = pair.to.Icao })
                .ToList(),
        };
        return Task.FromResult(plan);
    }

    public Task<TripNarrative> NarrateAsync(IReadOnlyList<TripLeg> legs, string userQuery, CancellationToken ct = default)
    {
        NarrateCallCount++;
        return Task.FromResult(NarrativeToReturn);
    }
}
