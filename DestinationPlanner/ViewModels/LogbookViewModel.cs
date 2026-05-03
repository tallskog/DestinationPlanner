using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace DestinationPlanner.ViewModels;

public class LogbookViewModel : ViewModelBase
{
    private readonly ILogbookService _logbook;

    private DateTime? _filterFromDate;
    private DateTime? _filterToDate;
    private string _filterAircraftTypeString = "All";
    private string _filterDepartureIcao = string.Empty;
    private string _filterArrivalIcao = string.Empty;
    private string _statusText = "No logbook loaded";

    public DateTime? FilterFromDate
    {
        get => _filterFromDate;
        set { SetField(ref _filterFromDate, value); ApplyFilters(); }
    }

    public DateTime? FilterToDate
    {
        get => _filterToDate;
        set { SetField(ref _filterToDate, value); ApplyFilters(); }
    }

    public string FilterAircraftTypeString
    {
        get => _filterAircraftTypeString;
        set { SetField(ref _filterAircraftTypeString, value); ApplyFilters(); }
    }

    public string FilterDepartureIcao
    {
        get => _filterDepartureIcao;
        set { SetField(ref _filterDepartureIcao, value); ApplyFilters(); }
    }

    public string FilterArrivalIcao
    {
        get => _filterArrivalIcao;
        set { SetField(ref _filterArrivalIcao, value); ApplyFilters(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string[] AircraftTypeOptions { get; } = ["All", "Airplane", "Helicopter"];
    public ObservableCollection<FlightRecord> FilteredFlights { get; } = [];

    public ICommand ImportNativeCommand { get; }
    public ICommand ImportForeignCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public LogbookViewModel(ILogbookService logbook)
    {
        _logbook = logbook;
        _logbook.FlightsChanged += (_, _) => ApplyFilters();

        ImportNativeCommand  = new RelayCommand(ImportNative);
        ImportForeignCommand = new RelayCommand(ImportForeign);
        ExportCommand        = new RelayCommand(Export, () => _logbook.Flights.Count > 0);
        ClearFiltersCommand  = new RelayCommand(ClearFilters);

        ApplyFilters();
    }

    // US15.1 – import native logbook: creates a new logbook file in AppData
    private void ImportNative()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Import Logbook",
            Filter = "Logbook XML (*.xml)|*.xml|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        string newPath = AppDataHelper.GetNewLogbookPath(DateTime.Now);
        _logbook.LoadInto(dlg.FileName, newPath);
        ApplyFilters();

        int count = _logbook.Flights.Count;
        MessageBox.Show($"Imported {count} flight{(count == 1 ? "" : "s")} into new logbook\n{System.IO.Path.GetFileName(newPath)}.",
                        "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ImportForeign()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Import Foreign Logbook",
            Filter = "Logbook XML (*.xml)|*.xml|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        int added = _logbook.ImportForeign(dlg.FileName);
        ApplyFilters();
        MessageBox.Show($"Imported {added} new flight{(added == 1 ? "" : "s")}.",
                        "Import complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // US15.2 – export to user-chosen location (does not affect the auto-save logbook)
    private void Export()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Export Logbook",
            Filter     = "Logbook XML (*.xml)|*.xml",
            DefaultExt = "xml",
            FileName   = System.IO.Path.GetFileName(_logbook.CurrentFilePath ?? "logbook.xml")
        };
        if (dlg.ShowDialog() != true) return;
        _logbook.Export(dlg.FileName);
    }

    private void ClearFilters()
    {
        _filterFromDate            = null;
        _filterToDate              = null;
        _filterAircraftTypeString  = "All";
        _filterDepartureIcao       = string.Empty;
        _filterArrivalIcao         = string.Empty;

        OnPropertyChanged(nameof(FilterFromDate));
        OnPropertyChanged(nameof(FilterToDate));
        OnPropertyChanged(nameof(FilterAircraftTypeString));
        OnPropertyChanged(nameof(FilterDepartureIcao));
        OnPropertyChanged(nameof(FilterArrivalIcao));

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<FlightRecord> results = _logbook.Flights;

        if (_filterFromDate.HasValue)
        {
            var from = DateOnly.FromDateTime(_filterFromDate.Value);
            results = results.Where(f => f.Date >= from);
        }
        if (_filterToDate.HasValue)
        {
            var to = DateOnly.FromDateTime(_filterToDate.Value);
            results = results.Where(f => f.Date <= to);
        }
        if (_filterAircraftTypeString != "All" &&
            Enum.TryParse<AircraftType>(_filterAircraftTypeString, out var type))
        {
            results = results.Where(f => f.AircraftType == type);
        }
        if (!string.IsNullOrWhiteSpace(_filterDepartureIcao))
        {
            var dep = _filterDepartureIcao.Trim().ToUpperInvariant();
            results = results.Where(f => f.DepartureIcao.Contains(dep, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(_filterArrivalIcao))
        {
            var arr = _filterArrivalIcao.Trim().ToUpperInvariant();
            results = results.Where(f => f.ArrivalIcao.Contains(arr, StringComparison.OrdinalIgnoreCase));
        }

        var list = results.OrderBy(f => f.BlockOffUtc).ToList();
        FilteredFlights.Clear();
        foreach (var f in list)
            FilteredFlights.Add(f);

        int total = _logbook.Flights.Count;
        int shown = FilteredFlights.Count;
        string file = _logbook.CurrentFilePath is string p
            ? $" — {System.IO.Path.GetFileName(p)}"
            : string.Empty;

        StatusText = total == 0
            ? $"No flights{file}"
            : shown == total
                ? $"{total} flight{(total == 1 ? "" : "s")}{file}"
                : $"{shown} of {total} flight{(total == 1 ? "" : "s")} shown{file}";
    }
}
