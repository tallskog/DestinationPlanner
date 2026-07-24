using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
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

    // OpenAIP airport-type integration (US34). Fully optional — see OpenAipCredentials.
    public IOpenAipDataService OpenAipData { get; }

    // Re-applied after every AirportDataService.LoadAsync call, since LoadAsync rebuilds
    // the airport dictionary from scratch and would otherwise wipe classifications.
    public IReadOnlyDictionary<string, AirportType>? LastAppliedOpenAipTypesByIcao { get; set; }

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
        OpenAipData = new OpenAipDataService();

        var sim = new SimConnectService(AirportData, settings.SimDataRateHz);
        sim.FlightCompleted   += (_, record) => logbook.AddFlight(record);
        sim.ConnectionChanged += (_, _) =>
            SimStatus = sim.IsConnected ? "MSFS: Connected" : "MSFS: Not connected";
        SimConnect = sim;

        Logbook = new LogbookViewModel(logbook, settings);
        Map     = new MapViewModel(AirportData, logbook, sim, settings);
    }
}
