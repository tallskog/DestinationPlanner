namespace DestinationPlanner.Models;

// ARINC 424 field 5.177, "Public/Military Indicator", as surfaced by Navigraph's
// DFD v2 tbl_pa_airports.airport_type column ('C'/'M'/'P').
public enum AirportType
{
    Unclassified, // no Navigraph data has ever been applied to this airport
    Civil,        // code 'C'
    Military,     // code 'M'
    Private,      // code 'P'
    Unknown,      // Navigraph data present but the code wasn't recognized
}
