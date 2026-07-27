namespace DestinationPlanner.Helpers;

// Curated aviation region name -> ICAO prefixes (US41). Sent to Claude as closed-set reference
// data when parsing a free-text trip query, so it selects a region's prefixes from this
// human-vetted table instead of recalling ICAO codes from memory (which it can get wrong).
public static class RegionLookup
{
    public static readonly IReadOnlyDictionary<string, string[]> Regions = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["Nordics"] = ["EN", "EF", "EK", "ES", "BI"],
        ["Scandinavia"] = ["EN", "EK", "ES"],
        ["Baltics"] = ["EV", "EY", "EE"],
        ["British Isles"] = ["EG", "EI"],
        ["Benelux"] = ["EB", "EH", "EL"],
        ["DACH"] = ["ED", "LO", "LS"],
        ["Iberia"] = ["LE", "LP"],
        ["France"] = ["LF"],
        ["Italy"] = ["LI"],
        ["Eastern Europe"] = ["EP", "LZ", "LH", "LR", "LB", "LW", "LY"],
        ["Northern Europe"] = ["EN", "EF", "EK", "ES", "BI", "EV", "EY", "EE"],
    };
}
