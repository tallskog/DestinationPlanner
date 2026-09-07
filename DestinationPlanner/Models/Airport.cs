namespace DestinationPlanner.Models;

public class Airport
{
    public string Icao { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int LongestRunwayFt { get; set; }
    public bool HasInstrumentApproach { get; set; }
    public bool HasAtis { get; set; }
    public AirportType Type { get; set; } = AirportType.Unclassified;
    public List<Runway> Runways { get; set; } = [];

    // User-entered correction layered on top of the OurAirports/OpenAIP-derived fields above —
    // see Services/AirportOverrideService and Models/AirportOverride. Deliberately never
    // touches Name/Type/HasAtis/HasInstrumentApproach/LongestRunwayFt directly: those keep
    // meaning "detected value" so a reset (clearing this) needs no recomputation.
    public AirportOverride? Override { get; set; }

    public AirportType EffectiveType                  => Override?.Type ?? Type;
    public string      EffectiveName                  => Override?.Name ?? Name;
    public bool        EffectiveHasAtis               => Override?.HasAtis ?? HasAtis;
    public bool        EffectiveHasInstrumentApproach => Override?.HasInstrumentApproach ?? HasInstrumentApproach;
    public int         EffectiveLongestRunwayFt       => Override?.LongestRunwayFt ?? LongestRunwayFt;

    // Forces this airport out of every filtered view regardless of other active filters —
    // see AirportFilterService.ApplyRunwayTypeAndPrefixFilters/ResolveIcaoOverrides.
    public bool IsHidden => Override?.IsHidden ?? false;
}
