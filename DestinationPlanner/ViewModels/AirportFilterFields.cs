namespace DestinationPlanner.ViewModels;

// Snapshot of AirportFilterViewModel's bindable fields for loading/saving to whichever
// AppSettings fields the owning tab (Map or Trip Plans) maps them to. Kept separate from
// AirportFilterCriteria because it also carries UseMeters (a display-only concern, converted
// to feet before criteria is built) and ShowVisited/ShowNotVisited (not part of the shared
// filter criteria — handled by each consumer's own visited-airport logic).
public sealed record AirportFilterFields
{
    public int MinRunway { get; init; }
    public int MaxRunway { get; init; }
    public bool UseMeters { get; init; }
    public bool RequireInstrumentApproach { get; init; }
    public bool RequireAtis { get; init; }
    public string FilterCenterIcao { get; init; } = string.Empty;
    public double FilterRadiusNm { get; init; }
    public bool ShowVisited { get; init; } = true;
    public bool ShowNotVisited { get; init; } = true;
    public bool ShowCivilAirports { get; init; } = true;
    public bool ShowMilitaryAirports { get; init; } = true;
    public bool ShowHeliportAirports { get; init; } = true;
    public bool ShowPrivateAirports { get; init; } = true;
    public bool ShowOtherAirports { get; init; } = true;
    public bool ShowUnknownAirports { get; init; } = true;
    public bool ShowUnclassifiedAirports { get; init; } = true;
    public string IcaoPrefixes { get; init; } = string.Empty;
}
