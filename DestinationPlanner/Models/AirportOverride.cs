namespace DestinationPlanner.Models;

// A user-entered correction layered on top of OurAirports/OpenAIP data for one airport,
// persisted separately (airport-overrides.json) so the cached OpenAIP data and OurAirports
// CSVs are never modified. Every field is nullable: null means "no override for this field,
// use the detected value." See Airport's Effective* properties/IsHidden for how these are
// merged at read time.
public class AirportOverride
{
    public AirportType? Type { get; set; }
    public string? Name { get; set; }
    public bool? HasAtis { get; set; }
    public bool? HasInstrumentApproach { get; set; }
    public int? LongestRunwayFt { get; set; }

    // Forces the airport out of every filtered view (Map tab, Trip Plan candidates),
    // regardless of other active filters — see AirportFilterService.
    public bool? IsHidden { get; set; }

    public bool IsEmpty =>
        Type is null && Name is null && HasAtis is null &&
        HasInstrumentApproach is null && LongestRunwayFt is null && IsHidden is null;
}
