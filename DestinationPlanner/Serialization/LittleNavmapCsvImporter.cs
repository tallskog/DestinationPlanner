using DestinationPlanner.Models;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DestinationPlanner.Serialization;

// Imports the Little Navmap CSV logbook export format.
// Timestamps include timezone offsets (e.g. "2025-01-15T17:42:35.082+02:00") and are
// converted to UTC via DateTimeOffset.  Airport identifiers that look like coordinates
// (e.g. "4951N00835E") are skipped — they are user-defined waypoints, not airports.
public static partial class LittleNavmapCsvImporter
{
    // Matches Little Navmap coordinate-style idents: 4 digits, N/S, 3–5 digits, E/W
    [GeneratedRegex(@"^\d{4}[NS]\d{3,5}[EW]$", RegexOptions.IgnoreCase)]
    private static partial Regex CoordinatePattern();

    public static IEnumerable<FlightRecord> Import(string filePath)
    {
        using var reader = new StreamReader(filePath);

        var headerLine = reader.ReadLine();
        if (headerLine is null) yield break;

        var headers = SplitCsvLine(headerLine);
        var idx = BuildIndex(headers);

        // Required columns — abort if the file doesn't look like a LNM export
        if (!idx.ContainsKey("Departure Ident")  ||
            !idx.ContainsKey("Destination Ident") ||
            !idx.ContainsKey("Departure Time")    ||
            !idx.ContainsKey("Destination Time"))
            throw new NotSupportedException("File does not appear to be a Little Navmap logbook CSV.");

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = SplitCsvLine(line);

            var dep = GetField(fields, idx, "Departure Ident").ToUpperInvariant();
            var arr = GetField(fields, idx, "Destination Ident").ToUpperInvariant();

            if (string.IsNullOrEmpty(dep) || string.IsNullOrEmpty(arr) || dep == arr)
                continue;
            if (CoordinatePattern().IsMatch(dep) || CoordinatePattern().IsMatch(arr))
                continue;

            var depTimeStr = GetField(fields, idx, "Departure Time");
            var arrTimeStr = GetField(fields, idx, "Destination Time");

            if (!DateTimeOffset.TryParse(depTimeStr, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var blockOff)) continue;
            if (!DateTimeOffset.TryParse(arrTimeStr, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var blockOn)) continue;

            var blockOffUtc = blockOff.UtcDateTime;
            var blockOnUtc  = blockOn.UtcDateTime;

            var model = BuildAircraftModel(
                            GetField(fields, idx, "Aircraft Name"),
                            GetField(fields, idx, "Aircraft Type"));

            yield return new FlightRecord
            {
                Date          = DateOnly.FromDateTime(blockOffUtc),
                AircraftModel = model,
                DepartureIcao = dep,
                ArrivalIcao   = arr,
                BlockOffUtc   = blockOffUtc,
                BlockOnUtc    = blockOnUtc,
            };
        }
    }

    // Build a cleaned, human-readable aircraft model string from the two name columns.
    private static string BuildAircraftModel(string rawName, string rawType)
    {
        var name = CleanAircraftField(rawName);
        var type = CleanAircraftField(rawType);
        return $"{name} {type}".Trim();
    }

    private static string CleanAircraftField(string value)
    {
        value = value.Trim();

        // Strip the "$$:" prefix that Little Navmap uses for some custom entries
        if (value.StartsWith("$$:", StringComparison.Ordinal))
            value = value[3..].Trim();

        // Raw simulator ATC name strings — not human-readable, discard them
        if (value.StartsWith("ATCCOM",   StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("AIRCRAFT", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return value;
    }

    private static Dictionary<string, int> BuildIndex(string[] headers)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
            dict.TryAdd(headers[i].Trim(), i);
        return dict;
    }

    private static string GetField(string[] fields, Dictionary<string, int> idx, string name)
        => idx.TryGetValue(name, out int i) && i < fields.Length ? fields[i].Trim() : string.Empty;

    // Minimal RFC-4180-compliant CSV line splitter (handles double-quoted fields).
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Escaped quote ""
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}
