using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.ViewModels;

public class MapViewModel : ViewModelBase
{
    private readonly IAirportDataService _airports;
    private readonly ILogbookService _logbook;
    private readonly ISimConnectService _sim;
    private readonly AppSettings _settings;

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

    // Shared with TripPlanViewModel's own independent instance (US43) — see AirportFilterViewModel.
    public AirportFilterViewModel Filters { get; }

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

        Filters = new AirportFilterViewModel(LoadFiltersFromSettings, SaveFiltersToSettings);
        Filters.Changed += (_, _) => FiltersApplied?.Invoke(this, EventArgs.Empty);

        _logbook.FlightsChanged += (_, _) => LogbookChanged?.Invoke(this, EventArgs.Empty);

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

        var criteria = Filters.BuildCriteria();
        IEnumerable<Airport>? candidates;

        if (!string.IsNullOrWhiteSpace(Filters.FilterCenterIcao) && Filters.FilterRadiusNm > 0)
        {
            candidates = AirportFilterService.ApplyCenterRadius(_airports, Filters.FilterCenterIcao, Filters.FilterRadiusNm);
            if (candidates is null) return [];
        }
        else
        {
            candidates = _airports.GetAll();
        }

        candidates = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(candidates, criteria);

        if (!Filters.ShowVisited || !Filters.ShowNotVisited)
        {
            var visited = _logbook.Flights
                .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!Filters.ShowVisited && !Filters.ShowNotVisited) candidates = [];
            else if (!Filters.ShowVisited) candidates = candidates.Where(a => !visited.Contains(a.Icao));
            else                           candidates = candidates.Where(a =>  visited.Contains(a.Icao));
        }

        var overrides = AirportFilterService.ResolveIcaoOverrides(_airports, criteria.IcaoPrefixes);
        return candidates.UnionBy(overrides, a => a.Icao, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Airports in the logbook — looked up by ICAO for lat/lon.
    // All active filters (radius, runway, ILS, ATIS, ICAO prefix) are applied so the
    // orange dots stay consistent with what the airport layer shows.
    public IReadOnlyList<Airport> GetLogbookAirports()
    {
        if (!_airports.IsLoaded || !Filters.ShowVisited) return [];

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
        if (!_airports.IsLoaded || !Filters.ShowVisited) return [];

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
        if (!_airports.IsLoaded || !Filters.ShowVisited) return [];

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
        if (!string.IsNullOrWhiteSpace(Filters.FilterCenterIcao) && Filters.FilterRadiusNm > 0)
        {
            candidates = AirportFilterService.ApplyCenterRadius(candidates, _airports, Filters.FilterCenterIcao, Filters.FilterRadiusNm);
            if (candidates is null) return null;
        }

        return AirportFilterService.ApplyRunwayTypeAndPrefixFilters(candidates, Filters.BuildCriteria());
    }

    private AirportFilterFields LoadFiltersFromSettings() => new()
    {
        MinRunway = _settings.MinRunway,
        MaxRunway = _settings.MaxRunway,
        UseMeters = _settings.UseMeters,
        RequireInstrumentApproach = _settings.RequireInstrumentApproach,
        RequireAtis = _settings.RequireAtis,
        FilterCenterIcao = _settings.FilterCenterIcao,
        FilterRadiusNm = _settings.FilterRadiusNm,
        ShowVisited = _settings.ShowVisited,
        ShowNotVisited = _settings.ShowNotVisited,
        ShowCivilAirports = _settings.ShowCivilAirports,
        ShowMilitaryAirports = _settings.ShowMilitaryAirports,
        ShowHeliportAirports = _settings.ShowHeliportAirports,
        ShowPrivateAirports = _settings.ShowPrivateAirports,
        ShowOtherAirports = _settings.ShowOtherAirports,
        ShowUnknownAirports = _settings.ShowUnknownAirports,
        ShowUnclassifiedAirports = _settings.ShowUnclassifiedAirports,
        IcaoPrefixes = _settings.IcaoPrefixes,
    };

    private void SaveFiltersToSettings(AirportFilterFields f)
    {
        _settings.MinRunway = f.MinRunway;
        _settings.MaxRunway = f.MaxRunway;
        _settings.UseMeters = f.UseMeters;
        _settings.RequireInstrumentApproach = f.RequireInstrumentApproach;
        _settings.RequireAtis = f.RequireAtis;
        _settings.FilterCenterIcao = f.FilterCenterIcao;
        _settings.FilterRadiusNm = f.FilterRadiusNm;
        _settings.ShowVisited = f.ShowVisited;
        _settings.ShowNotVisited = f.ShowNotVisited;
        _settings.ShowCivilAirports = f.ShowCivilAirports;
        _settings.ShowMilitaryAirports = f.ShowMilitaryAirports;
        _settings.ShowHeliportAirports = f.ShowHeliportAirports;
        _settings.ShowPrivateAirports = f.ShowPrivateAirports;
        _settings.ShowOtherAirports = f.ShowOtherAirports;
        _settings.ShowUnknownAirports = f.ShowUnknownAirports;
        _settings.ShowUnclassifiedAirports = f.ShowUnclassifiedAirports;
        _settings.IcaoPrefixes = f.IcaoPrefixes;
        AppSettingsService.Save(_settings);
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
