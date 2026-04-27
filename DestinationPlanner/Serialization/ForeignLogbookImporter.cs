using DestinationPlanner.Models;
using System.Globalization;
using System.Xml.Linq;

namespace DestinationPlanner.Serialization;

// Imports the ArrayOfFlightRecord XML format produced by the external logbook app.
// Date format: d.M.yyyy  (e.g. "23.3.2026")
// Time format: H.mm      (e.g. "16.53", "5.09") — dot separator, 24-hour, no leading zero
// Times are treated as local wall-clock time and stored as UTC.
public static class ForeignLogbookImporter
{
    public static IEnumerable<FlightRecord> Import(string filePath)
    {
        var doc = XDocument.Load(filePath);

        foreach (var el in doc.Root?.Elements("FlightRecord") ?? [])
        {
            var dep = (el.Element("From")?.Value ?? string.Empty).Trim().ToUpperInvariant();
            var arr = (el.Element("To")?.Value   ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(dep) || string.IsNullOrEmpty(arr) || dep == arr)
                continue;

            var dateStr      = el.Element("Date")?.Value          ?? string.Empty;
            var depTimeStr   = el.Element("DepartureTime")?.Value  ?? string.Empty;
            var arrTimeStr   = el.Element("ArrivalTime")?.Value    ?? string.Empty;
            var typeStr      = el.Element("Type")?.Value           ?? string.Empty;

            if (!TryParseDate(dateStr, out var date)) continue;
            if (!TryParseTime(depTimeStr, out var depTime)) continue;
            if (!TryParseTime(arrTimeStr, out var arrTime)) continue;

            var blockOff = DateTime.SpecifyKind(date.ToDateTime(depTime), DateTimeKind.Local).ToUniversalTime();
            var arrDate  = arrTime < depTime ? date.AddDays(1) : date; // handle midnight crossing
            var blockOn  = DateTime.SpecifyKind(arrDate.ToDateTime(arrTime), DateTimeKind.Local).ToUniversalTime();

            var aircraftType = typeStr.Equals("HELICOPTER", StringComparison.OrdinalIgnoreCase)
                ? AircraftType.Helicopter
                : AircraftType.Airplane;

            yield return new FlightRecord
            {
                Date          = date,
                AircraftType  = aircraftType,
                DepartureIcao = dep,
                ArrivalIcao   = arr,
                BlockOffUtc   = blockOff,
                BlockOnUtc    = blockOn,
            };
        }
    }

    // Parses "d.M.yyyy" — e.g. "23.3.2026", "6.4.2026"
    private static bool TryParseDate(string s, out DateOnly result)
        => DateOnly.TryParseExact(s.Trim(), "d.M.yyyy", CultureInfo.InvariantCulture,
                                  DateTimeStyles.None, out result);

    // Parses "H.mm" — e.g. "16.53", "5.09", "7.34"
    private static bool TryParseTime(string s, out TimeOnly result)
    {
        result = default;
        var parts = s.Trim().Split('.');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m)) return false;
        if (h < 0 || h > 23 || m < 0 || m > 59) return false;
        result = new TimeOnly(h, m);
        return true;
    }
}
