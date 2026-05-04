using DestinationPlanner.Models;
using System.IO;
using System.Text;

namespace DestinationPlanner.Services;

// Loads OurAirports.com data files:
//   airports.csv  — https://ourairports.com/data/airports.csv
//   runways.csv   — https://ourairports.com/data/runways.csv  (optional)
public class AirportDataService : IAirportDataService
{
    private Dictionary<string, Airport> _byIcao = [];
    private List<Airport> _all = [];

    public bool IsLoaded { get; private set; }
    public int Count => _all.Count;

    public async Task LoadAsync(string airportsCsvPath, string? runwaysCsvPath = null, string? frequenciesCsvPath = null)
    {
        var airports = await Task.Run(() => ParseAirports(airportsCsvPath));

        if (runwaysCsvPath != null && File.Exists(runwaysCsvPath))
            await Task.Run(() => ApplyRunwayData(airports, runwaysCsvPath));

        if (frequenciesCsvPath != null && File.Exists(frequenciesCsvPath))
            await Task.Run(() => ApplyFrequencyData(airports, frequenciesCsvPath));

        _all = airports.Values.ToList();
        _byIcao = airports;
        IsLoaded = true;
    }

    public Airport? GetByIcao(string icao) =>
        _byIcao.TryGetValue(icao.ToUpperInvariant(), out var a) ? a : null;

    public IReadOnlyList<Airport> GetAll() => _all;

    public IReadOnlyList<Airport> GetInBounds(double minLat, double maxLat, double minLon, double maxLon)
    {
        var result = new List<Airport>();
        foreach (var a in _all)
        {
            if (a.Latitude >= minLat && a.Latitude <= maxLat &&
                a.Longitude >= minLon && a.Longitude <= maxLon)
                result.Add(a);
        }
        return result;
    }

    // ---- Parsing ----

    private static Dictionary<string, Airport> ParseAirports(string path)
    {
        var dict = new Dictionary<string, Airport>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(path, Encoding.UTF8);
        string? header = reader.ReadLine(); // skip header
        if (header is null) return dict;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var f = SplitCsv(line);
            if (f.Length < 6) continue;

            var ident = f[1].Trim();
            if (string.IsNullOrEmpty(ident) || ident.Length < 2) continue;

            if (!double.TryParse(f[4], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double lat)) continue;
            if (!double.TryParse(f[5], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double lon)) continue;

            var type = f[2].Trim();
            bool hasIls = type is "large_airport" or "medium_airport"
                          || (type == "small_airport" && f.Length > 11 && f[11].Trim() == "yes");

            dict[ident] = new Airport
            {
                Icao = ident,
                Name = f[3].Trim(),
                Latitude = lat,
                Longitude = lon,
                HasInstrumentApproach = hasIls,
            };
        }

        return dict;
    }

    private static void ApplyRunwayData(Dictionary<string, Airport> airports, string path)
    {
        // OurAirports runways.csv columns:
        // 0:id, 1:airport_ref, 2:airport_ident, 3:length_ft, 4:width_ft, 5:surface,
        // 6:lighted, 7:closed, 8:le_ident, ..., 14:he_ident
        var runwaysByAirport = new Dictionary<string, List<(string ident, int lengthFt)>>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(path, Encoding.UTF8);
        reader.ReadLine(); // skip header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var f = SplitCsv(line);
            if (f.Length < 9) continue;

            var airportIdent = f[2].Trim();
            if (!int.TryParse(f[3].Trim(), out int lengthFt) || lengthFt <= 0) continue;

            // Skip closed runways
            if (f[7].Trim() == "1") continue;

            var leIdent = f[8].Trim();
            var heIdent = f.Length > 14 ? f[14].Trim() : string.Empty;
            var runwayIdent = string.IsNullOrEmpty(heIdent) ? leIdent : $"{leIdent}/{heIdent}";

            if (!runwaysByAirport.TryGetValue(airportIdent, out var list))
            {
                list = [];
                runwaysByAirport[airportIdent] = list;
            }
            list.Add((runwayIdent, lengthFt));
        }

        foreach (var (airportIdent, runways) in runwaysByAirport)
        {
            if (!airports.TryGetValue(airportIdent, out var airport)) continue;
            airport.Runways = runways
                .OrderByDescending(r => r.lengthFt)
                .Select(r => new Models.Runway { Ident = r.ident, LengthFt = r.lengthFt })
                .ToList();
            airport.LongestRunwayFt = airport.Runways[0].LengthFt;
        }
    }

    // airport-frequencies.csv columns: id, airport_ref, airport_ident, type, description, frequency_mhz
    private static void ApplyFrequencyData(Dictionary<string, Airport> airports, string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8);
        reader.ReadLine(); // skip header

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var f = SplitCsv(line);
            if (f.Length < 4) continue;

            var ident = f[2].Trim();
            var type  = f[3].Trim();

            if (string.Equals(type, "ATIS", StringComparison.OrdinalIgnoreCase) &&
                airports.TryGetValue(ident, out var airport))
            {
                airport.HasAtis = true;
            }
        }
    }

    // Minimal RFC-4180 CSV splitter (handles quoted fields, no escaped quotes inside quotes).
    private static string[] SplitCsv(string line)
    {
        var fields = new List<string>();
        var buf = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')       { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { fields.Add(buf.ToString()); buf.Clear(); }
            else                { buf.Append(c); }
        }
        fields.Add(buf.ToString());
        return fields.ToArray();
    }
}
