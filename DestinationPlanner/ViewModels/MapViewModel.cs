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
    private readonly AppSettings _settings;

    private int _minRunway;
    private int _maxRunway;
    private bool _useMeters;
    private bool _requireInstrumentApproach;
    private bool _requireAtis;
    private string _filterCenterIcao = string.Empty;
    private double _filterRadiusNm;
    private bool _showVisited = true;
    private bool _showNotVisited = true;
    private bool _showCivilAirports = true;
    private bool _showMilitaryAirports = true;
    private bool _showHeliportAirports = true;
    private bool _showPrivateAirports = true;
    private bool _showOtherAirports = true;
    private bool _showUnknownAirports = true;
    private bool _showUnclassifiedAirports = true;
    private string _icaoPrefixes = string.Empty;
    private string _airportDataStatus = "Airport data not loaded";
    private string _searchText = string.Empty;
    private IReadOnlyList<Airport> _searchResults = [];

    // Aircraft position (updated every SimConnect frame)
    private double _aircraftLat;
    private double _aircraftLon;
    private double _aircraftHeading;

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
    public bool ShowCivilAirports { get => _showCivilAirports; set => SetField(ref _showCivilAirports, value); }
    public bool ShowMilitaryAirports { get => _showMilitaryAirports; set => SetField(ref _showMilitaryAirports, value); }
    public bool ShowHeliportAirports { get => _showHeliportAirports; set => SetField(ref _showHeliportAirports, value); }
    public bool ShowPrivateAirports { get => _showPrivateAirports; set => SetField(ref _showPrivateAirports, value); }
    public bool ShowOtherAirports { get => _showOtherAirports; set => SetField(ref _showOtherAirports, value); }
    public bool ShowUnknownAirports { get => _showUnknownAirports; set => SetField(ref _showUnknownAirports, value); }
    public bool ShowUnclassifiedAirports { get => _showUnclassifiedAirports; set => SetField(ref _showUnclassifiedAirports, value); }
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
    public event EventHandler<AircraftPositionEventArgs>? AircraftMoved;

    public MapViewModel(IAirportDataService airports, ILogbookService logbook, ISimConnectService sim, AppSettings settings)
    {
        _airports = airports;
        _logbook  = logbook;
        _sim      = sim;
        _settings = settings;

        LoadFiltersFromSettings();

        _logbook.FlightsChanged += (_, _) => LogbookChanged?.Invoke(this, EventArgs.Empty);
        ApplyFiltersCommand = new RelayCommand(OnApplyFilters);
        ClearFiltersCommand = new RelayCommand(OnClearFilters);

        _sim.ConnectionChanged += OnSimConnectionChanged;
        _sim.FlightStarted     += OnFlightStarted;
        _sim.OnGroundChanged   += OnOnGroundChanged;
        _sim.FlightCompleted   += OnFlightCompleted;
        _sim.PositionChanged   += OnPositionChanged;

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

        IEnumerable<Airport>? candidates;

        if (!string.IsNullOrWhiteSpace(_filterCenterIcao) && _filterRadiusNm > 0)
        {
            candidates = AirportFilterService.ApplyCenterRadius(_airports, _filterCenterIcao, _filterRadiusNm);
            if (candidates is null) return [];
        }
        else
        {
            candidates = _airports.GetAll();
        }

        candidates = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(candidates, BuildFilterCriteria());

        if (!_showVisited || !_showNotVisited)
        {
            var visited = _logbook.Flights
                .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!_showVisited && !_showNotVisited) return [];
            if (!_showVisited)    candidates = candidates.Where(a => !visited.Contains(a.Icao));
            else                  candidates = candidates.Where(a =>  visited.Contains(a.Icao));
        }

        return candidates.ToList();
    }

    // Airports in the logbook — looked up by ICAO for lat/lon.
    // All active filters (radius, runway, ILS, ATIS, ICAO prefix) are applied so the
    // orange dots stay consistent with what the airport layer shows.
    public IReadOnlyList<Airport> GetLogbookAirports()
    {
        if (!_airports.IsLoaded || !_showVisited) return [];

        IEnumerable<Airport> candidates = _logbook.Flights
            .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(icao => _airports.GetByIcao(icao))
            .OfType<Airport>();

        candidates = ApplySharedFilters(candidates);
        if (candidates is null) return [];

        return candidates.ToList();
    }

    public IReadOnlyList<Airport> GetDepartedAirports()
    {
        if (!_airports.IsLoaded || !_showVisited) return [];

        IEnumerable<Airport> candidates = _logbook.Flights
            .Select(f => f.DepartureIcao)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(icao => _airports.GetByIcao(icao))
            .OfType<Airport>();

        candidates = ApplySharedFilters(candidates);
        if (candidates is null) return [];

        return candidates.ToList();
    }

    public IReadOnlyList<Airport> GetLandedAirports()
    {
        if (!_airports.IsLoaded || !_showVisited) return [];

        IEnumerable<Airport> candidates = _logbook.Flights
            .Select(f => f.ArrivalIcao)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(icao => _airports.GetByIcao(icao))
            .OfType<Airport>();

        candidates = ApplySharedFilters(candidates);
        if (candidates is null) return [];

        return candidates.ToList();
    }

    // Applies radius, runway, ILS, ATIS, and ICAO-prefix filters shared by all logbook layers.
    // Returns null when the radius center ICAO is configured but not found in airport data.
    private IEnumerable<Airport>? ApplySharedFilters(IEnumerable<Airport> candidates)
    {
        if (!string.IsNullOrWhiteSpace(_filterCenterIcao) && _filterRadiusNm > 0)
        {
            candidates = AirportFilterService.ApplyCenterRadius(candidates, _airports, _filterCenterIcao, _filterRadiusNm);
            if (candidates is null) return null;
        }

        return AirportFilterService.ApplyRunwayTypeAndPrefixFilters(candidates, BuildFilterCriteria());
    }

    // Builds the shared AirportFilterCriteria from this ViewModel's bound fields, converting
    // runway length to feet up front (UseMeters is a Map-tab display concern, not part of the
    // shared criteria). FilterCenterIcao/FilterRadiusNm are handled separately by the two
    // call sites above (via AirportFilterService.ApplyCenterRadius), not through this criteria.
    private AirportFilterCriteria BuildFilterCriteria() => new()
    {
        MinRunwayFt = UseMeters ? GeoHelper.MetersToFeet(MinRunway) : MinRunway,
        MaxRunwayFt = UseMeters ? GeoHelper.MetersToFeet(MaxRunway) : MaxRunway,
        RequireInstrumentApproach = RequireInstrumentApproach,
        RequireAtis = RequireAtis,
        IcaoPrefixes = ParseIcaoPrefixes(_icaoPrefixes),
        ShowCivilAirports = _showCivilAirports,
        ShowMilitaryAirports = _showMilitaryAirports,
        ShowHeliportAirports = _showHeliportAirports,
        ShowPrivateAirports = _showPrivateAirports,
        ShowOtherAirports = _showOtherAirports,
        ShowUnknownAirports = _showUnknownAirports,
        ShowUnclassifiedAirports = _showUnclassifiedAirports,
    };

    private static IReadOnlyList<string> ParseIcaoPrefixes(string raw) =>
        raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
           .Where(s => s.Length > 0)
           .ToList();

    private void LoadFiltersFromSettings()
    {
        _minRunway = _settings.MinRunway;
        _maxRunway = _settings.MaxRunway;
        _useMeters = _settings.UseMeters;
        _requireInstrumentApproach = _settings.RequireInstrumentApproach;
        _requireAtis = _settings.RequireAtis;
        _filterCenterIcao = _settings.FilterCenterIcao;
        _filterRadiusNm = _settings.FilterRadiusNm;
        _showVisited = _settings.ShowVisited;
        _showNotVisited = _settings.ShowNotVisited;
        _showCivilAirports = _settings.ShowCivilAirports;
        _showMilitaryAirports = _settings.ShowMilitaryAirports;
        _showHeliportAirports = _settings.ShowHeliportAirports;
        _showPrivateAirports = _settings.ShowPrivateAirports;
        _showOtherAirports = _settings.ShowOtherAirports;
        _showUnknownAirports = _settings.ShowUnknownAirports;
        _showUnclassifiedAirports = _settings.ShowUnclassifiedAirports;
        _icaoPrefixes = _settings.IcaoPrefixes;
    }

    private void SaveFiltersToSettings()
    {
        _settings.MinRunway = MinRunway;
        _settings.MaxRunway = MaxRunway;
        _settings.UseMeters = UseMeters;
        _settings.RequireInstrumentApproach = RequireInstrumentApproach;
        _settings.RequireAtis = RequireAtis;
        _settings.FilterCenterIcao = FilterCenterIcao;
        _settings.FilterRadiusNm = FilterRadiusNm;
        _settings.ShowVisited = ShowVisited;
        _settings.ShowNotVisited = ShowNotVisited;
        _settings.ShowCivilAirports = ShowCivilAirports;
        _settings.ShowMilitaryAirports = ShowMilitaryAirports;
        _settings.ShowHeliportAirports = ShowHeliportAirports;
        _settings.ShowPrivateAirports = ShowPrivateAirports;
        _settings.ShowOtherAirports = ShowOtherAirports;
        _settings.ShowUnknownAirports = ShowUnknownAirports;
        _settings.ShowUnclassifiedAirports = ShowUnclassifiedAirports;
        _settings.IcaoPrefixes = IcaoPrefixes;
        AppSettingsService.Save(_settings);
    }

    private void OnApplyFilters()
    {
        SaveFiltersToSettings();
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }

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
        ShowCivilAirports = true;
        ShowMilitaryAirports = true;
        ShowHeliportAirports = true;
        ShowPrivateAirports = true;
        ShowOtherAirports = true;
        ShowUnknownAirports = true;
        ShowUnclassifiedAirports = true;
        IcaoPrefixes = string.Empty;
        SaveFiltersToSettings();
        FiltersApplied?.Invoke(this, EventArgs.Empty);
    }

    private void OnPositionChanged(object? sender, AircraftPositionEventArgs e)
    {
        _aircraftLat     = e.Latitude;
        _aircraftLon     = e.Longitude;
        _aircraftHeading = e.HeadingDegrees;
        AircraftMoved?.Invoke(this, e);
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
            // Fire AircraftMoved so the view hides the marker.
            AircraftMoved?.Invoke(this, new AircraftPositionEventArgs(
                _aircraftLat, _aircraftLon, _aircraftHeading));
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
