using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using System.Windows.Input;

namespace DestinationPlanner.ViewModels;

public class MapViewModel : ViewModelBase
{
    private readonly IAirportDataService _airports;
    private readonly ILogbookService _logbook;
    private readonly ISimConnectService _sim;

    private int _minRunway;
    private int _maxRunway;
    private bool _useMeters;
    private bool _requireInstrumentApproach;
    private bool _requireAtis;
    private string _filterCenterIcao = string.Empty;
    private double _filterRadiusNm;
    private bool _showVisited = true;
    private bool _showNotVisited = true;
    private string _icaoPrefixes = string.Empty;
    private string _airportDataStatus = "Airport data not loaded";
    private string _searchText = string.Empty;
    private IReadOnlyList<Airport> _searchResults = [];

    // SimConnect flight status
    private bool   _simConnected;
    private string _simStatusText = "MSFS: Not connected";
    private string _flightInfoText = string.Empty;
    private string _departureIcao  = string.Empty;
    private string _blockOffZulu   = string.Empty;
    private string _flightPhase    = string.Empty;
    private string _arrivalIcao    = string.Empty;
    private string _blockOnZulu    = string.Empty;
    private string _aircraftModel  = string.Empty;

    public bool   SimConnected   { get => _simConnected;   private set => SetField(ref _simConnected,   value); }
    public string SimStatusText  { get => _simStatusText;  private set => SetField(ref _simStatusText,  value); }
    public string FlightInfoText { get => _flightInfoText; private set => SetField(ref _flightInfoText, value); }

    public int MinRunway { get => _minRunway; set => SetField(ref _minRunway, value); }
    public int MaxRunway { get => _maxRunway; set => SetField(ref _maxRunway, value); }
    public bool UseMeters { get => _useMeters; set => SetField(ref _useMeters, value); }
    public bool RequireInstrumentApproach { get => _requireInstrumentApproach; set => SetField(ref _requireInstrumentApproach, value); }
    public bool RequireAtis               { get => _requireAtis;               set => SetField(ref _requireAtis,               value); }
    public string FilterCenterIcao { get => _filterCenterIcao; set => SetField(ref _filterCenterIcao, value); }
    public double FilterRadiusNm { get => _filterRadiusNm; set => SetField(ref _filterRadiusNm, value); }
    public bool ShowVisited { get => _showVisited; set => SetField(ref _showVisited, value); }
    public bool ShowNotVisited { get => _showNotVisited; set => SetField(ref _showNotVisited, value); }
    public string IcaoPrefixes { get => _icaoPrefixes; set => SetField(ref _icaoPrefixes, value); }

    public string AirportDataStatus
    {
        get => _airportDataStatus;
        private set => SetField(ref _airportDataStatus, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                SearchResults = RunSearch(value);
        }
    }

    public IReadOnlyList<Airport> SearchResults
    {
        get => _searchResults;
        private set => SetField(ref _searchResults, value);
    }

    private IReadOnlyList<Airport> RunSearch(string text)
    {
        if (!_airports.IsLoaded || string.IsNullOrWhiteSpace(text))
            return [];
        var q = text.Trim();
        return _airports.GetAll()
            .Where(a => a.Icao.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                     || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Icao.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(a => a.Icao)
            .Take(20)
            .ToList();
    }

    public ICommand ApplyFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    // The view subscribes to these to know when to refresh layers.
    public event EventHandler? FiltersApplied;
    public event EventHandler? LogbookChanged;

    public MapViewModel(IAirportDataService airports, ILogbookService logbook, ISimConnectService sim)
    {
        _airports = airports;
        _logbook  = logbook;
        _sim      = sim;

        _logbook.FlightsChanged += (_, _) => LogbookChanged?.Invoke(this, EventArgs.Empty);
        ApplyFiltersCommand = new RelayCommand(OnApplyFilters);
        ClearFiltersCommand = new RelayCommand(OnClearFilters);

        _sim.ConnectionChanged += OnSimConnectionChanged;
        _sim.FlightStarted     += OnFlightStarted;
        _sim.OnGroundChanged   += OnOnGroundChanged;
        _sim.FlightCompleted   += OnFlightCompleted;

        // Reflect initial connection state.
        SimConnected  = _sim.IsConnected;
        SimStatusText = _sim.IsConnected ? "MSFS: Connected" : "MSFS: Not connected";
    }

    public Airport? GetAirportByIcao(string icao) => _airports.GetByIcao(icao);

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

        if (RequireAtis)
            candidates = candidates.Where(a => a.HasAtis);

        int minFt = UseMeters ? GeoHelper.MetersToFeet(MinRunway) : MinRunway;
        int maxFt = UseMeters ? GeoHelper.MetersToFeet(MaxRunway) : MaxRunway;

        if (minFt > 0) candidates = candidates.Where(a => a.LongestRunwayFt >= minFt);
        if (maxFt > 0) candidates = candidates.Where(a => a.LongestRunwayFt <= maxFt);

        if (!_showVisited || !_showNotVisited)
        {
            var visited = _logbook.Flights
                .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!_showVisited && !_showNotVisited) return [];
            if (!_showVisited)    candidates = candidates.Where(a => !visited.Contains(a.Icao));
            else                  candidates = candidates.Where(a =>  visited.Contains(a.Icao));
        }

        var prefixes = ParseIcaoPrefixes(_icaoPrefixes);
        if (prefixes.Count > 0)
            candidates = candidates.Where(a => prefixes.Any(p => a.Icao.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        return candidates.ToList();
    }

    // Airports in the logbook — looked up by ICAO for lat/lon.
    // Radius filter is applied when active; runway/ILS filters are not (those are destination-search criteria).
    public IReadOnlyList<Airport> GetLogbookAirports()
    {
        if (!_airports.IsLoaded || !_showVisited) return [];

        IEnumerable<Airport> candidates = _logbook.Flights
            .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(icao => _airports.GetByIcao(icao))
            .OfType<Airport>();

        if (!string.IsNullOrWhiteSpace(_filterCenterIcao) && _filterRadiusNm > 0)
        {
            var center = _airports.GetByIcao(_filterCenterIcao.Trim());
            if (center is null) return [];
            candidates = candidates
                .Where(a => GeoHelper.DistanceNm(center.Latitude, center.Longitude, a.Latitude, a.Longitude) <= _filterRadiusNm);
        }

        var prefixes = ParseIcaoPrefixes(_icaoPrefixes);
        if (prefixes.Count > 0)
            candidates = candidates.Where(a => prefixes.Any(p => a.Icao.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        return candidates.ToList();
    }

    private static IReadOnlyList<string> ParseIcaoPrefixes(string raw) =>
        raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
           .Where(s => s.Length > 0)
           .ToList();

    private void OnApplyFilters() => FiltersApplied?.Invoke(this, EventArgs.Empty);

    private void OnClearFilters()
    {
        MinRunway = 0;
        MaxRunway = 0;
        UseMeters = false;
        RequireInstrumentApproach = false;
        RequireAtis = false;
        FilterCenterIcao = string.Empty;
        FilterRadiusNm = 0;
        ShowVisited = true;
        ShowNotVisited = true;
        IcaoPrefixes = string.Empty;
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }

    private void OnSimConnectionChanged(object? sender, EventArgs e)
    {
        SimConnected  = _sim.IsConnected;
        SimStatusText = _sim.IsConnected ? "MSFS: Connected" : "MSFS: Not connected";
        if (!_sim.IsConnected)
        {
            _departureIcao = string.Empty;
            _blockOffZulu  = string.Empty;
            _flightPhase   = string.Empty;
            _arrivalIcao   = string.Empty;
            _blockOnZulu   = string.Empty;
            _aircraftModel = string.Empty;
            UpdateFlightInfoText();
        }
    }

    private void OnFlightStarted(object? sender, FlightStartedEventArgs e)
    {
        _departureIcao = e.DepartureIcao;
        _blockOffZulu  = e.BlockOffUtc.ToString("HH:mm:ss");
        _aircraftModel = e.AircraftModel;
        _arrivalIcao   = string.Empty;
        _blockOnZulu   = string.Empty;
        _flightPhase   = string.Empty;
        UpdateFlightInfoText();
    }

    private void OnOnGroundChanged(object? sender, bool isOnGround)
    {
        _flightPhase = isOnGround ? "On Ground" : "Airborne";
        UpdateFlightInfoText();
    }

    private void OnFlightCompleted(object? sender, FlightRecord e)
    {
        _arrivalIcao = e.ArrivalIcao;
        _blockOnZulu = e.BlockOnUtc.ToString("HH:mm:ss");
        _flightPhase = "On Ground";
        UpdateFlightInfoText();
    }

    private void UpdateFlightInfoText()
    {
        if (string.IsNullOrEmpty(_departureIcao))
        {
            FlightInfoText = string.Empty;
            return;
        }

        var parts = new List<string> { $"DEP: {_departureIcao}" };
        if (!string.IsNullOrEmpty(_aircraftModel))  parts.Add(_aircraftModel);
        if (!string.IsNullOrEmpty(_blockOffZulu)) parts.Add($"Off: {_blockOffZulu}Z");
        if (!string.IsNullOrEmpty(_flightPhase))  parts.Add(_flightPhase);
        if (!string.IsNullOrEmpty(_arrivalIcao))  parts.Add($"ARR: {_arrivalIcao}");
        if (!string.IsNullOrEmpty(_blockOnZulu))  parts.Add($"On: {_blockOnZulu}Z");

        FlightInfoText = "  ·  " + string.Join("  ·  ", parts);
    }
}
