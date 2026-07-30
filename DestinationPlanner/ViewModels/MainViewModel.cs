using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using System.Windows;

namespace DestinationPlanner.ViewModels;

public class MainViewModel : ViewModelBase
{
    public LogbookViewModel Logbook { get; }
    public MapViewModel Map { get; }
    public TripPlanViewModel TripPlan { get; }

    // Exposed so MainWindow can call LoadAsync and then notify MapViewModel.
    public IAirportDataService AirportData { get; }

    // The raw logbook service (as opposed to the Logbook ViewModel above) — needed by
    // TripCandidateService (US41) for visited-airport lookups.
    public ILogbookService LogbookData { get; }

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

        LogbookData = logbook;
        var tripCandidates = new TripCandidateService(AirportData, LogbookData);
        TripPlan = new TripPlanViewModel(
            AirportData,
            tripCandidates,
            LogbookData,
            settings,
            isAiConfigured: () => AnthropicCredentials.TryLoad() is not null,
            createAiService: () => new AnthropicTripPlanningService(AnthropicCredentials.TryLoad()!.ApiKey));
    }
}
