using System.Collections.ObjectModel;
using System.Windows.Input;
using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.ViewModels;

// AI-assisted trip planning (US41). Generation flow: ParseQueryAsync (AI) -> map to
// AirportFilterCriteria -> ITripCandidateService.GetCandidates (deterministic) -> user reviews
// Candidates -> ConfirmCommand runs PlanTripAsync (AI) -> result is saved via TripPlanStore.
//
// isAiConfigured/createAiService are injected rather than resolved from AnthropicCredentials
// directly, so (a) tests can substitute a FakeAiTripPlanningService without touching real
// AppData or the network, and (b) credentials are re-checked on every call rather than baked
// in once at startup — saving a new key via "Configure AI..." takes effect immediately.
public class TripPlanViewModel : ViewModelBase
{
    private readonly IAirportDataService _airportData;
    private readonly ITripCandidateService _candidateService;
    private readonly ILogbookService _logbook;
    private readonly AppSettings _settings;
    private readonly Func<bool> _isAiConfigured;
    private readonly Func<IAiTripPlanningService> _createAiService;

    private string _queryText = string.Empty;
    private string _startIcao = string.Empty;
    private string _statusText = string.Empty;
    private bool _isBusy;
    private AirportFilterCriteria? _lastCriteria;
    private bool _lastExcludeVisited = true;
    private double _lastMinLegNm;
    private double _lastMaxLegNm;
    private double _minLegNm;
    private double _maxLegNm;
    private TripPlan? _selectedPlan;
    private TripLegRow? _selectedLeg;
    private Airport? _selectedCandidate;
    private string _candidateSearchText = string.Empty;
    private IReadOnlyList<Airport> _candidateSearchResults = [];

    public string QueryText { get => _queryText; set => SetField(ref _queryText, value); }
    public string StartIcao { get => _startIcao; set => SetField(ref _startIcao, value); }
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }
    public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

    // Explicit per-leg distance bounds (US43 follow-up) — independent of the free-text query,
    // so a leg-distance constraint works reliably even when the candidate list was built
    // manually (filters/search) and "Generate Candidates" (the only place a query's leg-distance
    // phrasing gets AI-parsed into _lastMinLegNm/_lastMaxLegNm below) was never run, or would
    // otherwise wipe out those manually-added candidates. 0 means "not set"; when set, these take
    // precedence over whatever Generate last parsed from the query text (see ConfirmAsync).
    public double MinLegNm { get => _minLegNm; set => SetField(ref _minLegNm, value); }
    public double MaxLegNm { get => _maxLegNm; set => SetField(ref _maxLegNm, value); }

    // True once an Anthropic API key has been configured — the view hides/disables the
    // feature entirely when false.
    public bool IsAiConfigured => _isAiConfigured();

    // Exposed so the View's code-behind can open a map window for the selected plan (US41
    // follow-up) without the ViewModel itself depending on any WPF/Mapsui types.
    public IAirportDataService AirportData => _airportData;

    public TripPlan? SelectedPlan
    {
        get => _selectedPlan;
        set
        {
            if (!SetField(ref _selectedPlan, value)) return;
            RefreshSelectedPlanLegs();
        }
    }

    public TripLegRow? SelectedLeg { get => _selectedLeg; set => SetField(ref _selectedLeg, value); }
    public Airport? SelectedCandidate { get => _selectedCandidate; set => SetField(ref _selectedCandidate, value); }

    // Shared with MapViewModel's own independent instance (US43) — see AirportFilterViewModel.
    public AirportFilterViewModel Filters { get; }

    public string CandidateSearchText
    {
        get => _candidateSearchText;
        set
        {
            if (SetField(ref _candidateSearchText, value))
                CandidateSearchResults = RunCandidateSearch(value);
        }
    }

    public IReadOnlyList<Airport> CandidateSearchResults
    {
        get => _candidateSearchResults;
        private set => SetField(ref _candidateSearchResults, value);
    }

    public ObservableCollection<Airport> Candidates { get; } = [];
    public ObservableCollection<TripPlan> SavedPlans { get; } = [];
    public ObservableCollection<TripLegRow> SelectedPlanLegs { get; } = [];

    public ICommand GenerateCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand MarkLegFlownCommand { get; }
    public ICommand RemoveSelectedCandidateCommand { get; }
    public ICommand DeleteSelectedPlanCommand { get; }
    public ICommand ReuseQueryCommand { get; }
    public ICommand AddFilteredAirportsCommand { get; }

    public TripPlanViewModel(
        IAirportDataService airportData,
        ITripCandidateService candidateService,
        ILogbookService logbook,
        AppSettings settings,
        Func<bool> isAiConfigured,
        Func<IAiTripPlanningService> createAiService)
    {
        _airportData = airportData;
        _candidateService = candidateService;
        _logbook = logbook;
        _settings = settings;
        _isAiConfigured = isAiConfigured;
        _createAiService = createAiService;

        Filters = new AirportFilterViewModel(LoadFiltersFromSettings, SaveFiltersToSettings);

        foreach (var plan in TripPlanStore.LoadAll())
            SavedPlans.Add(plan);

        // Catches legs that already match a logged flight — e.g. the route was flown before
        // this plan was even generated, or before this auto-mark feature existed.
        if (AutoMarkFlownLegs())
            TripPlanStore.SaveAll(SavedPlans);

        // SimConnect adds a FlightRecord the moment a flight lands (MainViewModel wires
        // sim.FlightCompleted -> logbook.AddFlight), so this keeps a leg's status current
        // without the user needing to revisit this tab. Mirrors how MapViewModel/LogbookViewModel
        // already subscribe to FlightsChanged directly (no dispatcher marshaling needed — the
        // event is already raised on the UI thread by the time it reaches here).
        _logbook.FlightsChanged += (_, _) => OnFlightsChanged();

        GenerateCommand = new RelayCommand(async () => await GenerateAsync(), () => CanGenerate);
        ConfirmCommand = new RelayCommand(async () => await ConfirmAsync(), () => CanConfirm);
        MarkLegFlownCommand = new RelayCommand(MarkSelectedLegFlown, () => SelectedLeg is { Status: TripLegStatus.Planned });
        RemoveSelectedCandidateCommand = new RelayCommand(RemoveSelectedCandidate, () => SelectedCandidate is not null);
        DeleteSelectedPlanCommand = new RelayCommand(DeleteSelectedPlan, () => SelectedPlan is not null);
        ReuseQueryCommand = new RelayCommand(ReuseQuery, () => !string.IsNullOrEmpty(SelectedPlan?.Query));
        AddFilteredAirportsCommand = new RelayCommand(AddFilteredAirports, () => _airportData.IsLoaded);
    }

    private bool CanGenerate => !IsBusy && IsAiConfigured && _airportData.IsLoaded && !string.IsNullOrWhiteSpace(QueryText);
    private bool CanConfirm => !IsBusy && IsAiConfigured && Candidates.Count > 0;

    // Internal (not private) so tests can await generation/confirmation directly — RelayCommand
    // wraps these as async-void lambdas, which the ICommand interface can't be awaited through.
    internal async Task GenerateAsync()
    {
        if (!IsAiConfigured) return;

        IsBusy = true;
        StatusText = "Interpreting query...";
        try
        {
            var ai = _createAiService();
            var filters = await ai.ParseQueryAsync(QueryText);
            _lastCriteria = filters.ToFilterCriteria();
            _lastExcludeVisited = filters.ExcludeVisited;
            _lastMinLegNm = filters.MinLegDistanceNm;
            _lastMaxLegNm = filters.MaxLegDistanceNm;
            if (!string.IsNullOrWhiteSpace(filters.StartIcao))
                StartIcao = filters.StartIcao;

            var candidates = _candidateService.GetCandidates(_lastCriteria, _lastExcludeVisited);

            Candidates.Clear();
            foreach (var airport in candidates)
                Candidates.Add(airport);

            StatusText = candidates.Count == 0
                ? $"No matching airports found ({filters.IntentSummary})"
                : $"{candidates.Count} candidate airport(s) — {filters.IntentSummary}";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not interpret query: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            // RelayCommand's CanExecuteChanged is tied to CommandManager.RequerySuggested,
            // which only fires on WPF input events — not when IsBusy/Candidates change from
            // code resuming after an await. Without this, ConfirmCommand stays visually
            // disabled until some unrelated UI interaction (e.g. typing) happens to trigger
            // a requery, even though CanConfirm is already true.
            CommandManager.InvalidateRequerySuggested();
        }
    }

    internal async Task ConfirmAsync()
    {
        if (!IsAiConfigured || Candidates.Count == 0) return;

        IsBusy = true;
        StatusText = "Planning trip...";
        try
        {
            var ai = _createAiService();
            TripPlan plan;
            string? warning = null;

            // The explicit MinLegNm/MaxLegNm fields (set directly by the user) take precedence,
            // per field, over whatever Generate last parsed from the free-text query into
            // _lastMinLegNm/_lastMaxLegNm — so a constraint set this way is honored even if
            // Generate was never run (or was run before/after with different phrasing).
            double effectiveMinNm = MinLegNm > 0 ? MinLegNm : _lastMinLegNm;
            double effectiveMaxNm = MaxLegNm > 0 ? MaxLegNm : _lastMaxLegNm;

            if (effectiveMinNm > 0 || effectiveMaxNm > 0)
            {
                // A per-leg distance constraint was given. Trusting Claude to judge inter-airport
                // distances from bare ICAO codes means trusting its memorized geography instead
                // of real data — the same trap this app avoids everywhere else. Build the route
                // deterministically from actual coordinates instead, and only ask the AI to
                // narrate the (already fixed) result.
                double minNm = effectiveMinNm;
                double maxNm = effectiveMaxNm > 0 ? effectiveMaxNm : double.MaxValue;
                var candidateList = Candidates.ToList();
                var route = TripRouteBuilder.BuildRoute(candidateList, minNm, maxNm,
                    string.IsNullOrWhiteSpace(StartIcao) ? null : StartIcao);

                if (route.Count == 0)
                {
                    StatusText = $"No valid route found with legs between {FormatNm(minNm)} and {FormatNm(maxNm)} nm among the candidates.";
                    return;
                }

                var legs = route
                    .Select((r, i) => new TripLeg { Order = i + 1, DepartureIcao = r.From.Icao, ArrivalIcao = r.To.Icao })
                    .ToList();
                var narrative = await ai.NarrateAsync(legs, QueryText);
                plan = new TripPlan { Title = narrative.Title, Narrative = narrative.Narrative, Legs = legs };

                int visitedCount = legs.SelectMany(l => new[] { l.DepartureIcao, l.ArrivalIcao })
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int skipped = candidateList.Count - visitedCount;
                if (skipped > 0)
                    warning = $"; {skipped} candidate(s) could not be connected within the distance constraint";
            }
            else
            {
                plan = await ai.PlanTripAsync(Candidates.ToList(), QueryText, StartIcao);
            }

            // Recorded so the user can recall/reuse the query later (SelectedPlan's query is
            // shown read-only and can be copied back into QueryText via ReuseQueryCommand).
            plan.Query = QueryText;

            SavedPlans.Add(plan);
            TripPlanStore.SaveAll(SavedPlans);
            SelectedPlan = plan;
            StatusText = $"Trip plan \"{plan.Title}\" saved ({plan.Legs.Count} leg(s)){warning}";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not generate trip plan: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private static string FormatNm(double valueNm) => valueNm == double.MaxValue ? "any" : valueNm.ToString("N0");

    private void MarkSelectedLegFlown()
    {
        if (SelectedLeg is null) return;

        SelectedLeg.Leg.Status = TripLegStatus.Flown;
        RefreshSelectedPlanLegs();
        TripPlanStore.SaveAll(SavedPlans);
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnFlightsChanged()
    {
        if (!AutoMarkFlownLegs()) return;

        TripPlanStore.SaveAll(SavedPlans);
        RefreshSelectedPlanLegs();
        CommandManager.InvalidateRequerySuggested();
    }

    // A Planned leg becomes Flown the moment a logged flight matches its exact departure/arrival
    // ICAO pair — same "any flight ever logged" lookup TripCandidateService already uses for the
    // visited-airport check, not just flights added after the plan was created. Deliberately
    // one-directional: a leg already Flown (whether by this or by MarkLegFlownCommand) is never
    // reset back to Planned, even if the matching flight is later edited/removed from the logbook.
    private bool AutoMarkFlownLegs()
    {
        bool changed = false;
        foreach (var plan in SavedPlans)
        {
            foreach (var leg in plan.Legs)
            {
                if (leg.Status == TripLegStatus.Flown) continue;

                var match = _logbook.Flights.FirstOrDefault(f =>
                    string.Equals(f.DepartureIcao, leg.DepartureIcao, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(f.ArrivalIcao, leg.ArrivalIcao, StringComparison.OrdinalIgnoreCase));
                if (match is null) continue;

                leg.Status = TripLegStatus.Flown;
                leg.FlownFlightId = match.Id;
                changed = true;
            }
        }
        return changed;
    }

    private void RemoveSelectedCandidate()
    {
        if (SelectedCandidate is null) return;

        Candidates.Remove(SelectedCandidate);
        SelectedCandidate = null;
        CommandManager.InvalidateRequerySuggested();
    }

    // Non-AI path to building the candidate list (US43): runs the same deterministic
    // ITripCandidateService.GetCandidates pipeline GenerateAsync uses, but merges the results
    // into whatever's already in Candidates instead of replacing it — so filter-based additions,
    // an AI query, and manually-searched airports can all be combined.
    internal void AddFilteredAirports()
    {
        Filters.Persist();

        if (!Filters.ShowVisited && !Filters.ShowNotVisited)
        {
            StatusText = "No airports added (both visit-status checkboxes are unchecked).";
            return;
        }

        // TripCandidateService only supports a single "exclude visited" bool, not the Map tab's
        // independent show-visited/show-not-visited pair. Deriving excludeVisited from
        // !ShowVisited covers "show everything" and "hide visited"; asking for "visited only"
        // (only ShowNotVisited unchecked) isn't representable by the shared service and falls
        // back to "include everyone" rather than extending that service's contract for this one
        // edge case.
        bool excludeVisited = !Filters.ShowVisited;
        var found = _candidateService.GetCandidates(Filters.BuildCriteria(), excludeVisited);

        var existing = Candidates.Select(a => a.Icao).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var a in found)
            if (existing.Add(a.Icao)) { Candidates.Add(a); added++; }

        StatusText = added == 0
            ? $"No new candidates matched (found {found.Count}, all already in the list)."
            : $"Added {added} airport(s) ({found.Count - added} already present).";
        CommandManager.InvalidateRequerySuggested();
    }

    private IReadOnlyList<Airport> RunCandidateSearch(string text)
    {
        if (!_airportData.IsLoaded || string.IsNullOrWhiteSpace(text)) return [];
        var q = text.Trim();
        return _airportData.GetAll()
            .Where(a => a.Icao.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                     || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Icao.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(a => a.Icao)
            .Take(20)
            .ToList();
    }

    // Skips if already present (by ICAO, case-insensitive) — same merge rule as
    // AddFilteredAirports. Internal (not private) so the View's click/Enter handlers and tests
    // can call it directly.
    internal void AddCandidateAirport(Airport airport)
    {
        if (Candidates.Any(a => string.Equals(a.Icao, airport.Icao, StringComparison.OrdinalIgnoreCase))) return;

        Candidates.Add(airport);
        CandidateSearchText = string.Empty;
        CommandManager.InvalidateRequerySuggested();
    }

    private AirportFilterFields LoadFiltersFromSettings() => new()
    {
        MinRunway = _settings.TripPlanMinRunway,
        MaxRunway = _settings.TripPlanMaxRunway,
        UseMeters = _settings.TripPlanUseMeters,
        RequireInstrumentApproach = _settings.TripPlanRequireInstrumentApproach,
        RequireAtis = _settings.TripPlanRequireAtis,
        FilterCenterIcao = _settings.TripPlanFilterCenterIcao,
        FilterRadiusNm = _settings.TripPlanFilterRadiusNm,
        ShowVisited = _settings.TripPlanShowVisited,
        ShowNotVisited = _settings.TripPlanShowNotVisited,
        ShowCivilAirports = _settings.TripPlanShowCivilAirports,
        ShowMilitaryAirports = _settings.TripPlanShowMilitaryAirports,
        ShowHeliportAirports = _settings.TripPlanShowHeliportAirports,
        ShowPrivateAirports = _settings.TripPlanShowPrivateAirports,
        ShowOtherAirports = _settings.TripPlanShowOtherAirports,
        ShowUnknownAirports = _settings.TripPlanShowUnknownAirports,
        ShowUnclassifiedAirports = _settings.TripPlanShowUnclassifiedAirports,
        IcaoPrefixes = _settings.TripPlanIcaoPrefixes,
    };

    private void SaveFiltersToSettings(AirportFilterFields f)
    {
        _settings.TripPlanMinRunway = f.MinRunway;
        _settings.TripPlanMaxRunway = f.MaxRunway;
        _settings.TripPlanUseMeters = f.UseMeters;
        _settings.TripPlanRequireInstrumentApproach = f.RequireInstrumentApproach;
        _settings.TripPlanRequireAtis = f.RequireAtis;
        _settings.TripPlanFilterCenterIcao = f.FilterCenterIcao;
        _settings.TripPlanFilterRadiusNm = f.FilterRadiusNm;
        _settings.TripPlanShowVisited = f.ShowVisited;
        _settings.TripPlanShowNotVisited = f.ShowNotVisited;
        _settings.TripPlanShowCivilAirports = f.ShowCivilAirports;
        _settings.TripPlanShowMilitaryAirports = f.ShowMilitaryAirports;
        _settings.TripPlanShowHeliportAirports = f.ShowHeliportAirports;
        _settings.TripPlanShowPrivateAirports = f.ShowPrivateAirports;
        _settings.TripPlanShowOtherAirports = f.ShowOtherAirports;
        _settings.TripPlanShowUnknownAirports = f.ShowUnknownAirports;
        _settings.TripPlanShowUnclassifiedAirports = f.ShowUnclassifiedAirports;
        _settings.TripPlanIcaoPrefixes = f.IcaoPrefixes;
        AppSettingsService.Save(_settings);
    }

    private void DeleteSelectedPlan()
    {
        if (SelectedPlan is null) return;

        SavedPlans.Remove(SelectedPlan);
        TripPlanStore.SaveAll(SavedPlans);
        SelectedPlan = null;
        CommandManager.InvalidateRequerySuggested();
    }

    // Copies the selected plan's originating query back into the query box so the user can
    // review it (also shown read-only next to the saved-plans list) and/or tweak it and
    // regenerate. Does not restore StartIcao — only the free-text query was asked for.
    private void ReuseQuery()
    {
        if (SelectedPlan is null) return;
        QueryText = SelectedPlan.Query;
    }

    // Rebuilds the bound leg-row collection from the persisted TripLeg list, resolving each
    // leg's distance from whichever airport data is currently loaded (best-effort — a leg
    // whose airport isn't in the loaded dataset simply shows no distance rather than failing).
    private void RefreshSelectedPlanLegs()
    {
        SelectedPlanLegs.Clear();
        if (SelectedPlan is null) return;

        foreach (var leg in SelectedPlan.Legs)
        {
            var from = _airportData.GetByIcao(leg.DepartureIcao);
            var to = _airportData.GetByIcao(leg.ArrivalIcao);
            double? distanceNm = (from is not null && to is not null)
                ? GeoHelper.DistanceNm(from.Latitude, from.Longitude, to.Latitude, to.Longitude)
                : null;

            SelectedPlanLegs.Add(new TripLegRow(leg, distanceNm));
        }
    }
}
