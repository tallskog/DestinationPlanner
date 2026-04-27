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

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public LogbookViewModel(ILogbookService logbook)
    {
        _logbook = logbook;
        OpenCommand        = new RelayCommand(Open);
        SaveCommand        = new RelayCommand(Save, () => _logbook.Flights.Count > 0);
        ImportCommand      = new RelayCommand(Import);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    private void Open()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Open Logbook",
            Filter = "Logbook XML (*.xml)|*.xml|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        _logbook.Load(dlg.FileName);
        ApplyFilters();
    }

    private void Save()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Save Logbook",
            Filter     = "Logbook XML (*.xml)|*.xml",
            DefaultExt = "xml"
        };
        if (dlg.ShowDialog() != true) return;
        _logbook.Save(dlg.FileName);
    }

    private void Import()
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
        StatusText = total == 0
            ? "No logbook loaded"
            : shown == total
                ? $"{total} flight{(total == 1 ? "" : "s")}"
                : $"{shown} of {total} flight{(total == 1 ? "" : "s")} shown";
    }
}
