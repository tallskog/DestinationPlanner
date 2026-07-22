using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Fakes;

// Unused events are required by ISimConnectService but not exercised by current
// ViewModel tests, which don't depend on live SimConnect data.
#pragma warning disable CS0067
public class FakeSimConnectService : ISimConnectService
{
    public bool IsConnected => false;

    public event EventHandler<FlightRecord>? FlightCompleted;
    public event EventHandler<FlightStartedEventArgs>? FlightStarted;
    public event EventHandler<bool>? OnGroundChanged;
    public event EventHandler? ConnectionChanged;
    public event EventHandler<AircraftPositionEventArgs>? PositionChanged;

    public void Connect(nint windowHandle) { }
    public void Disconnect() { }
}
#pragma warning restore CS0067
