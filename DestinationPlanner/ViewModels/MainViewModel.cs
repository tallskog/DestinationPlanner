using DestinationPlanner.Helpers;
using DestinationPlanner.Services;
using System.Windows;

namespace DestinationPlanner.ViewModels;

public class MainViewModel : ViewModelBase
{
    public LogbookViewModel Logbook { get; }
    public MapViewModel Map { get; }

    // Exposed so MainWindow can call LoadAsync and then notify MapViewModel.
    public IAirportDataService AirportData { get; }

    public ISimConnectService SimConnect { get; }

    // Navigraph airport-type integration (US34). Fully optional — see NavigraphCredentials.
    public INavigraphAuthService NavigraphAuth { get; }
    public INavigraphDataService NavigraphData { get; }
    public NavigraphSessionState NavigraphSession { get; } = new();
    public AppSettings Settings { get; }

    private string _simStatus = "MSFS: Not connected";
    public string SimStatus { get => _simStatus; private set => SetField(ref _simStatus, value); }

    public MainViewModel(string logbookPath, AppSettings settings)
    {
        var logbook = new LogbookService();
        try
        {
            logbook.Load(logbookPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not load logbook:\n{ex.Message}\n\nStarting with an empty logbook. " +
                "The file may have been saved by a newer version of DestinationPlanner.",
                "Logbook Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        AirportData = new AirportDataService();
        Settings = settings;
        NavigraphAuth = new NavigraphAuthService(NavigraphCredentials.TryLoad());
        NavigraphData = new NavigraphDataService();

        var sim = new SimConnectService(AirportData, settings.SimDataRateHz);
        sim.FlightCompleted   += (_, record) => logbook.AddFlight(record);
        sim.ConnectionChanged += (_, _) =>
            SimStatus = sim.IsConnected ? "MSFS: Connected" : "MSFS: Not connected";
        SimConnect = sim;

        Logbook = new LogbookViewModel(logbook, settings);
        Map     = new MapViewModel(AirportData, logbook, sim);
    }
}
