using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface ISimConnectService
{
    bool IsConnected { get; }
    event EventHandler<FlightRecord>? FlightCompleted;
    event EventHandler? ConnectionChanged;
    void Connect(nint windowHandle);
    void Disconnect();
}
