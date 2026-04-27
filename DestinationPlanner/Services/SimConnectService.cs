using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// SimConnect integration — requires Microsoft.FlightSimulator.SimConnect reference
// from the MSFS SDK (add manually: SDK\SimConnect SDK\lib\static\SimConnect.dll).
public class SimConnectService : ISimConnectService
{
    public bool IsConnected { get; private set; }
#pragma warning disable CS0067  // raised once SimConnect wires up the event
    public event EventHandler<FlightRecord>? FlightCompleted;
#pragma warning restore CS0067

    public void Connect(nint windowHandle)
    {
        // TODO: initialise SimConnect, subscribe to PARKING_BRAKE sim variable
        IsConnected = true;
    }

    public void Disconnect()
    {
        // TODO: dispose SimConnect connection
        IsConnected = false;
    }
}
