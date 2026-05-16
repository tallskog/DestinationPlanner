using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public record FlightStartedEventArgs(string DepartureIcao, DateTime BlockOffUtc, string AircraftModel);
public record AircraftPositionEventArgs(double Latitude, double Longitude, double HeadingDegrees);

public interface ISimConnectService
{
    bool IsConnected { get; }
    event EventHandler<FlightRecord>? FlightCompleted;
    event EventHandler<FlightStartedEventArgs>? FlightStarted;
    event EventHandler<bool>? OnGroundChanged;   // true = on ground, false = airborne
    event EventHandler? ConnectionChanged;
    event EventHandler<AircraftPositionEventArgs>? PositionChanged;
    void Connect(nint windowHandle);
    void Disconnect();
}
