namespace DestinationPlanner.Models;

// Structured output of IAiTripPlanningService.ParseQueryAsync (US41) — a small subset of
// AirportFilterCriteria that Claude is allowed to fill in from a free-text query. Fields left
// unset map to AirportFilterCriteria's "no restriction" defaults (see ToFilterCriteria()).
public record TripQueryFilters
{
    public IReadOnlyList<string> IcaoPrefixes { get; init; } = [];
    public int MinRunwayFt { get; init; }
    public int MaxRunwayFt { get; init; }
    public string FilterCenterIcao { get; init; } = string.Empty;
    public double FilterRadiusNm { get; init; }
    public bool ExcludeVisited { get; init; } = true;
    public string StartIcao { get; init; } = string.Empty;

    // Per-leg distance bounds (0 = no bound). Not part of AirportFilterCriteria/airport
    // selection — these constrain how TripCandidateService's output is sequenced into legs
    // (see TripRouteBuilder), so they're consumed directly by TripPlanViewModel instead of
    // going through ToFilterCriteria().
    public double MinLegDistanceNm { get; init; }
    public double MaxLegDistanceNm { get; init; }

    public string IntentSummary { get; init; } = string.Empty;

    public AirportFilterCriteria ToFilterCriteria() => new()
    {
        IcaoPrefixes = IcaoPrefixes,
        MinRunwayFt = MinRunwayFt,
        MaxRunwayFt = MaxRunwayFt,
        FilterCenterIcao = string.IsNullOrWhiteSpace(FilterCenterIcao) ? null : FilterCenterIcao,
        FilterRadiusNm = FilterRadiusNm,
    };
}
