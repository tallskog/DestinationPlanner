namespace DestinationPlanner.Models;

// A selectable flight level for the wind barb overlay, mapped to the nearest
// Open-Meteo pressure level (ICAO standard atmosphere pressure-altitude conversion).
// See requirements.md US39.
public record WindFlightLevel(string Label, int PressureHPa)
{
    public static readonly IReadOnlyList<WindFlightLevel> Standard =
    [
        new("Surface", 1000),
        new("3,000 ft", 900),
        new("6,000 ft", 800),
        new("9,000 ft", 700),
        new("12,000 ft", 600),
        new("18,000 ft (FL180)", 500),
        new("24,000 ft (FL240)", 400),
        new("30,000 ft (FL300)", 300),
        new("34,000 ft (FL340)", 250),
        new("39,000 ft (FL390)", 200),
    ];
}
