namespace DestinationPlanner.Models;

// A single wind observation/forecast point at a specific pressure level.
// DirectionDeg is the meteorological convention (direction the wind is blowing FROM,
// 0-360, clockwise from true north).
public record WindSample(double Latitude, double Longitude, double DirectionDeg, double SpeedKt);
