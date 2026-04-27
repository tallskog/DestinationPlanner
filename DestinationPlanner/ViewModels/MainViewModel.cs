using DestinationPlanner.Services;

namespace DestinationPlanner.ViewModels;

public class MainViewModel : ViewModelBase
{
    public LogbookViewModel Logbook { get; }
    public MapViewModel Map { get; }

    // Exposed so MainWindow can call LoadAsync and then notify MapViewModel.
    public IAirportDataService AirportData { get; }

    public MainViewModel()
    {
        var logbookService = new LogbookService();
        AirportData = new AirportDataService();
        Logbook = new LogbookViewModel(logbookService);
        Map     = new MapViewModel(AirportData, logbookService);
    }
}
