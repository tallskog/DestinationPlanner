using DestinationPlanner.Models;
using System.Xml.Linq;

namespace DestinationPlanner.Serialization;

public static class NativeLogbookSerializer
{
    private const string Ns = "urn:destination-planner:logbook:v1";

    public static void Save(IEnumerable<FlightRecord> flights, string filePath)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(XName.Get("FlightLogbook", Ns),
                new XAttribute("version", "1.0"),
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
            yield return FromXml(el);
    }

    private static XElement ToXml(FlightRecord f) =>
        new(XName.Get("Flight", Ns),
            new XElement(XName.Get("Id", Ns), f.Id),
            new XElement(XName.Get("Date", Ns), f.Date.ToString("yyyy-MM-dd")),
            new XElement(XName.Get("AircraftModel", Ns), f.AircraftModel),
            new XElement(XName.Get("DepartureIcao", Ns), f.DepartureIcao),
            new XElement(XName.Get("ArrivalIcao", Ns), f.ArrivalIcao),
            new XElement(XName.Get("BlockOffUtc", Ns), f.BlockOffUtc.ToString("o")),
            new XElement(XName.Get("BlockOnUtc", Ns), f.BlockOnUtc.ToString("o")));

    private static FlightRecord FromXml(XElement el)
    {
        XName N(string local) => XName.Get(local, Ns);
        return new FlightRecord
        {
            Id            = Guid.Parse(el.Element(N("Id"))!.Value),
            Date          = DateOnly.ParseExact(el.Element(N("Date"))!.Value, "yyyy-MM-dd"),
            // AircraftType element is silently ignored when present in older files
            AircraftModel = el.Element(N("AircraftModel"))?.Value ?? string.Empty,
            DepartureIcao = el.Element(N("DepartureIcao"))!.Value,
            ArrivalIcao   = el.Element(N("ArrivalIcao"))!.Value,
            BlockOffUtc   = DateTime.Parse(el.Element(N("BlockOffUtc"))!.Value).ToUniversalTime(),
            BlockOnUtc    = DateTime.Parse(el.Element(N("BlockOnUtc"))!.Value).ToUniversalTime(),
        };
    }
}
