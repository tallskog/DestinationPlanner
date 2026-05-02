namespace DestinationPlanner.Models;

public class Airport
{
    public string Icao { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int LongestRunwayFt { get; set; }
    public bool HasInstrumentApproach { get; set; }
    public bool HasAtis { get; set; }
}
