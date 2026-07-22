using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface INavigraphDataService
{
    // Downloads the current DFD v2 navigation data package and returns the local .3sdb file path.
    Task<string> DownloadCurrentPackageAsync(string accessToken, CancellationToken ct);

    // Reads tbl_pa_airports.airport_type (ARINC 424 field 5.177) from the given
    // DFD v2 SQLite file, keyed by uppercase ICAO code.
    Dictionary<string, AirportType> ParseAirportTypes(string sqliteFilePath);
}
