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

    private string _simStatus = "MSFS: Not connected";
    public string SimStatus { get => _simStatus; private set => SetField(ref _simStatus, value); }

    public MainViewModel(string logbookPath)
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

        var sim = new SimConnectService(AirportData);
        sim.FlightCompleted   += (_, record) => logbook.AddFlight(record);
        sim.ConnectionChanged += (_, _) =>
            SimStatus = sim.IsConnected ? "MSFS: Connected" : "MSFS: Not connected";
        SimConnect = sim;

        Logbook = new LogbookViewModel(logbook);
        Map     = new MapViewModel(AirportData, logbook, sim);
    }
}
