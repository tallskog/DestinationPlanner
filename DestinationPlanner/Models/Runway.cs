namespace DestinationPlanner.Models;

public class Runway
{
    public string Ident { get; set; } = string.Empty;   // e.g. "09L/27R"
    public int LengthFt { get; set; }

    // Runway endpoint coordinates from OurAirports runways.csv (null if not in dataset)
    public double? LeLatitude  { get; set; }
    public double? LeLongitude { get; set; }
    public double? LeHeadingDeg { get; set; }
    public double? HeLatitude  { get; set; }
    public double? HeLongitude { get; set; }
    public double? HeHeadingDeg { get; set; }
}
