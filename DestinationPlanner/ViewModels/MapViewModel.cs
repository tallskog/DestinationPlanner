using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using System.Windows.Input;

namespace DestinationPlanner.ViewModels;

public class MapViewModel : ViewModelBase
{
    private readonly IAirportDataService _airports;
    private readonly ILogbookService _logbook;

    private int _minRunway;
    private int _maxRunway;
    private bool _useMeters;
    private bool _requireInstrumentApproach;
    private string _filterCenterIcao = string.Empty;
    private double _filterRadiusNm;
    private string _airportDataStatus = "Airport data not loaded";

    public int MinRunway { get => _minRunway; set => SetField(ref _minRunway, value); }
    public int MaxRunway { get => _maxRunway; set => SetField(ref _maxRunway, value); }
    public bool UseMeters { get => _useMeters; set => SetField(ref _useMeters, value); }
    public bool RequireInstrumentApproach { get => _requireInstrumentApproach; set => SetField(ref _requireInstrumentApproach, value); }
    public string FilterCenterIcao { get => _filterCenterIcao; set => SetField(ref _filterCenterIcao, value); }
    public double FilterRadiusNm { get => _filterRadiusNm; set => SetField(ref _filterRadiusNm, value); }

    public string AirportDataStatus
    {
        get => _airportDataStatus;
        private set => SetField(ref _airportDataStatus, value);
    }

    public ICommand ApplyFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    // The view subscribes to these to know when to refresh layers.
    public event EventHandler? FiltersApplied;
    public event EventHandler? LogbookChanged;

    public MapViewModel(IAirportDataService airports, ILogbookService logbook)
    {
        _airports = airports;
        _logbook = logbook;
        _logbook.FlightsChanged += (_, _) => LogbookChanged?.Invoke(this, EventArgs.Empty);
        ApplyFiltersCommand = new RelayCommand(OnApplyFilters);
        ClearFiltersCommand = new RelayCommand(OnClearFilters);
    }

    public void NotifyAirportDataLoaded()
    {
        AirportDataStatus = $"{_airports.Count:N0} airports loaded";
        FiltersApplied?.Invoke(this, EventArgs.Empty);
        LogbookChanged?.Invoke(this, EventArgs.Empty);
    }

    // Returns all airports matching the active filters (no viewport clipping —
    // Mapsui's MemoryLayer spatial index handles rendering only the visible ones).
    public IReadOnlyList<Airport> GetAllFilteredAirports()
    {
        if (!_airports.IsLoaded) return [];

        IEnumerable<Airport> candidates;

        if (!string.IsNullOrWhiteSpace(_filterCenterIcao) && _filterRadiusNm > 0)
        {
            var center = _airports.GetByIcao(_filterCenterIcao.Trim());
            if (center is null) return [];
            double degApprox = _filterRadiusNm / 60.0;
            candidates = _airports.GetInBounds(
                center.Latitude  - degApprox, center.Latitude  + degApprox,
                center.Longitude - degApprox, center.Longitude + degApprox)
                .Where(a => GeoHelper.DistanceNm(center.Latitude, center.Longitude, a.Latitude, a.Longitude) <= _filterRadiusNm);
        }
        else
        {
            candidates = _airports.GetAll();
        }

        if (RequireInstrumentApproach)
            candidates = candidates.Where(a => a.HasInstrumentApproach);

        int minFt = UseMeters ? GeoHelper.MetersToFeet(MinRunway) : MinRunway;
        int maxFt = UseMeters ? GeoHelper.MetersToFeet(MaxRunway) : MaxRunway;

        if (minFt > 0) candidates = candidates.Where(a => a.LongestRunwayFt >= minFt);
        if (maxFt > 0) candidates = candidates.Where(a => a.LongestRunwayFt <= maxFt);

        return candidates.ToList();
    }

    // Airports in the logbook — looked up by ICAO for lat/lon.
    public IReadOnlyList<Airport> GetLogbookAirports()
    {
        if (!_airports.IsLoaded) return [];
        return _logbook.Flights
            .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(icao => _airports.GetByIcao(icao))
            .OfType<Airport>()
            .ToList();
    }

    private void OnApplyFilters() => FiltersApplied?.Invoke(this, EventArgs.Empty);

    private void OnClearFilters()
    {
        MinRunway = 0;
        MaxRunway = 0;
        UseMeters = false;
        RequireInstrumentApproach = false;
        FilterCenterIcao = string.Empty;
        FilterRadiusNm = 0;
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }
}
