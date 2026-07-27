namespace DestinationPlanner.Models;

// Shared filter criteria used by the Map tab's airport search (MapViewModel) and by
// AI-assisted trip planning (TripCandidateService, US41). Defaults match AppSettings'
// "no restriction" defaults, so an unset criteria matches every airport.
public record AirportFilterCriteria
{
    public int MinRunwayFt { get; init; }
    public int MaxRunwayFt { get; init; }
    public bool RequireInstrumentApproach { get; init; }
    public bool RequireAtis { get; init; }
    public IReadOnlyList<string> IcaoPrefixes { get; init; } = [];
    public string? FilterCenterIcao { get; init; }
    public double FilterRadiusNm { get; init; }
    public bool ShowCivilAirports { get; init; } = true;
    public bool ShowMilitaryAirports { get; init; } = true;
    public bool ShowHeliportAirports { get; init; } = true;
    public bool ShowPrivateAirports { get; init; } = true;
    public bool ShowOtherAirports { get; init; } = true;
    public bool ShowUnknownAirports { get; init; } = true;
    public bool ShowUnclassifiedAirports { get; init; } = true;
}
