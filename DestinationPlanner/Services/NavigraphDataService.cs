using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Downloads Navigraph's DFD v2 navigation data package and extracts the
// airport_type classification (ARINC 424 field 5.177) from tbl_pa_airports.
//
// NOTE: the exact 'format' query value and the JSON field names for the signed
// file URL / AIRAC cycle below are best-effort guesses based on published docs —
// they are unconfirmed until real API access is granted and will likely need a
// small fix-up against the real /v1/navdata/packages response shape.
public class NavigraphDataService : INavigraphDataService
{
    private const string PackagesEndpoint = "https://api.navigraph.com/v1/navdata/packages";
    private const string DfdV2Format = "dfdv2";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public async Task<string> DownloadCurrentPackageAsync(string accessToken, CancellationToken ct)
    {
        string navigraphDir = Path.Combine(AppDataHelper.AppDataPath, "navigraph");
        Directory.CreateDirectory(navigraphDir);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{PackagesEndpoint}?format={DfdV2Format}&package_status=current");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        string body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var packages = doc.RootElement.GetProperty("packages");
        if (packages.GetArrayLength() == 0)
            throw new InvalidOperationException("Navigraph returned no current navigation data package.");

        var package = packages[0];
        string cycle = package.TryGetProperty("cycle", out var c) ? c.GetString() ?? "unknown" : "unknown";
        var file = package.GetProperty("files")[0];
        string url = file.GetProperty("signed_url").GetString()!;

        string destPath = Path.Combine(navigraphDir, $"ng_jeppesen_fwdfd_{cycle}.3sdb");

        var bytes = await _http.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(destPath, bytes, ct);

        PruneOldPackages(navigraphDir, keep: destPath);

        return destPath;
    }

    public Dictionary<string, AirportType> ParseAirportTypes(string sqliteFilePath)
    {
        var result = new Dictionary<string, AirportType>(StringComparer.OrdinalIgnoreCase);

        using var connection = new SqliteConnection($"Data Source={sqliteFilePath};Mode=ReadOnly");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT icao_code, airport_type FROM tbl_pa_airports";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0)) continue;
            string icao = reader.GetString(0).Trim();
            if (icao.Length == 0) continue;

            string? code = reader.IsDBNull(1) ? null : reader.GetString(1).Trim();
            result[icao] = code switch
            {
                "C" => AirportType.Civil,
                "M" => AirportType.Military,
                "P" => AirportType.Private,
                _ => AirportType.Unknown,
            };
        }

        return result;
    }

    private static void PruneOldPackages(string navigraphDir, string keep)
    {
        foreach (var file in Directory.GetFiles(navigraphDir, "*.3sdb"))
        {
            if (!string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(file); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
