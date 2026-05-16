using DestinationPlanner.Models;
using System.Globalization;
using System.Xml.Linq;

namespace DestinationPlanner.Serialization;

public static class NativeLogbookSerializer
{
    private const string Ns      = "urn:destination-planner:logbook:v1";
    private const string Version = "1.1";

    public static void Save(IEnumerable<FlightRecord> flights, string filePath)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(XName.Get("FlightLogbook", Ns),
                new XAttribute("version", Version),
                new XElement(XName.Get("Flights", Ns),
                    flights.Select(ToXml))));

        doc.Save(filePath);
    }

    public static IEnumerable<FlightRecord> Load(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var flightsEl = doc.Root?.Element(XName.Get("Flights", Ns));
        if (flightsEl is null) yield break;

        foreach (var el in flightsEl.Elements(XName.Get("Flight", Ns)))
        {
            FlightRecord? record = null;
            try { record = FromXml(el); } catch { /* skip malformed record */ }
            if (record is not null) yield return record;
        }
    }

    private static XElement ToXml(FlightRecord f)
    {
        XName N(string local) => XName.Get(local, Ns);
        return new XElement(N("Flight"),
            new XElement(N("Id"),            f.Id),
            new XElement(N("Date"),          f.Date.ToString("yyyy-MM-dd")),
            new XElement(N("AircraftModel"), f.AircraftModel),
            new XElement(N("DepartureIcao"), f.DepartureIcao),
            new XElement(N("ArrivalIcao"),   f.ArrivalIcao),
            new XElement(N("BlockOffUtc"),   f.BlockOffUtc.ToString("o")),
            new XElement(N("BlockOnUtc"),    f.BlockOnUtc.ToString("o")),
            // Landing stats — omitted when null so old-version files stay valid
            f.LandingFpm.HasValue           ? new XElement(N("LandingFpm"),          F1(f.LandingFpm.Value))           : null,
            f.LandingGForce.HasValue        ? new XElement(N("LandingGForce"),       F2(f.LandingGForce.Value))        : null,
            f.LandingAirspeedKts.HasValue   ? new XElement(N("LandingAirspeedKts"),  F1(f.LandingAirspeedKts.Value))   : null,
            f.LandingWindKts.HasValue       ? new XElement(N("LandingWindKts"),      F1(f.LandingWindKts.Value))       : null,
            f.LandingWindDirection.HasValue ? new XElement(N("LandingWindDir"),      F0(f.LandingWindDirection.Value)) : null);
    }

    private static FlightRecord FromXml(XElement el)
    {
        XName N(string local) => XName.Get(local, Ns);
        return new FlightRecord
        {
            Id            = Guid.Parse(el.Element(N("Id"))!.Value),
            Date          = DateOnly.ParseExact(el.Element(N("Date"))!.Value, "yyyy-MM-dd"),
            // AircraftType element is silently ignored when present in older files
            AircraftModel = el.Element(N("AircraftModel"))?.Value ?? string.Empty,
            DepartureIcao = el.Element(N("DepartureIcao"))?.Value ?? string.Empty,
            ArrivalIcao   = el.Element(N("ArrivalIcao"))?.Value   ?? string.Empty,
            BlockOffUtc   = DateTime.Parse(el.Element(N("BlockOffUtc"))!.Value).ToUniversalTime(),
            BlockOnUtc    = DateTime.Parse(el.Element(N("BlockOnUtc"))!.Value).ToUniversalTime(),
            // Landing stats — absent in older files; parsed as null
            LandingFpm           = ParseNullDouble(el.Element(N("LandingFpm"))?.Value),
            LandingGForce        = ParseNullDouble(el.Element(N("LandingGForce"))?.Value),
            LandingAirspeedKts   = ParseNullDouble(el.Element(N("LandingAirspeedKts"))?.Value),
            LandingWindKts       = ParseNullDouble(el.Element(N("LandingWindKts"))?.Value),
            LandingWindDirection = ParseNullDouble(el.Element(N("LandingWindDir"))?.Value),
        };
    }

    private static double? ParseNullDouble(string? s)
        => s is null ? null : double.Parse(s, CultureInfo.InvariantCulture);

    private static string F0(double v) => v.ToString("F0", CultureInfo.InvariantCulture);
    private static string F1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    private static string F2(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
}
