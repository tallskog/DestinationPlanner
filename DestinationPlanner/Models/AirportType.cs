namespace DestinationPlanner.Models;

// Sourced from OpenAIP's /airports 'type' (0-13) and 'private' fields — see
// OpenAipDataService.MapType for the full mapping.
public enum AirportType
{
    Unclassified, // no OpenAIP record has ever been applied to this airport
    Civil,        // type 0, 2, 3, or 9
    Military,     // type 5
    Heliport,     // type 4 or 7 (military or civil)
    Private,      // OpenAIP 'private' flag, regardless of type
    Other,        // type 1, 6, 8, 10, 11, 12, or 13 (glider/ultralight/water/landing strip/agricultural/altiport/closed)
    Unknown,      // OpenAIP record present but 'type' is null/missing
}
