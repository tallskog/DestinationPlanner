using DestinationPlanner.Services;

namespace DestinationPlanner.ViewModels;

public class MainViewModel : ViewModelBase
{
    public LogbookViewModel Logbook { get; }
    public MapViewModel Map { get; }

    // Exposed so MainWindow can call LoadAsync and then notify MapViewModel.
    public IAirportDataService AirportData { get; }

    public ISimConnectService SimConnect { get; }

    private string _simStatus = "MSFS: Not connected";
    public string SimStatus { get => _simStatus; private set => SetField(ref _simStatus, value); }

    public MainViewModel()
    {
        var logbook  = new LogbookService();
        AirportData  = new AirportDataService();

        var sim = new SimConnectService(AirportData);
        sim.FlightCompleted  += (_, record) => logbook.AddFlight(record);
        sim.ConnectionChanged += (_, _) =>
            SimStatus = sim.IsConnected ? "MSFS: Connected" : "MSFS: Not connected";
        SimConnect = sim;

        Logbook = new LogbookViewModel(logbook);
        Map     = new MapViewModel(AirportData, logbook);
    }
}
