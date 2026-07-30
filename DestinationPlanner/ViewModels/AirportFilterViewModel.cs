using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using System.Windows.Input;

namespace DestinationPlanner.ViewModels;

// Shared airport-filter bindable state (US43), used independently by both MapViewModel (Map
// tab) and TripPlanViewModel (Trip Plans tab) — one instance per tab, each with its own
// load/save delegates into its own AppSettings fields, so the two tabs' filter values never
// share state. Extracted from MapViewModel, which previously owned all of this directly.
public class AirportFilterViewModel : ViewModelBase
{
    private readonly Action<AirportFilterFields> _save;

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

    public ICommand ApplyFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    // Raised after Apply or Clear persists — the owning ViewModel re-raises its own
    // tab-specific event from this (e.g. MapViewModel.FiltersApplied) so existing View
    // code-behind subscriptions don't need to change their event source.
    public event EventHandler? Changed;

    public AirportFilterViewModel(Func<AirportFilterFields> load, Action<AirportFilterFields> save)
    {
        _save = save;

        var f = load();
        _minRunway = f.MinRunway;
        _maxRunway = f.MaxRunway;
        _useMeters = f.UseMeters;
        _requireInstrumentApproach = f.RequireInstrumentApproach;
        _requireAtis = f.RequireAtis;
        _filterCenterIcao = f.FilterCenterIcao;
        _filterRadiusNm = f.FilterRadiusNm;
        _showVisited = f.ShowVisited;
        _showNotVisited = f.ShowNotVisited;
        _showCivilAirports = f.ShowCivilAirports;
        _showMilitaryAirports = f.ShowMilitaryAirports;
        _showHeliportAirports = f.ShowHeliportAirports;
        _showPrivateAirports = f.ShowPrivateAirports;
        _showOtherAirports = f.ShowOtherAirports;
        _showUnknownAirports = f.ShowUnknownAirports;
        _showUnclassifiedAirports = f.ShowUnclassifiedAirports;
        _icaoPrefixes = f.IcaoPrefixes;

        ApplyFiltersCommand = new RelayCommand(OnApply);
        ClearFiltersCommand = new RelayCommand(OnClear);
    }

    // Unlike MapViewModel's previous Map-only BuildFilterCriteria (which omitted
    // FilterCenterIcao/FilterRadiusNm because Map applies center-radius itself via a separate
    // AirportFilterService.ApplyCenterRadius pre-filter step), this shared version always
    // includes them — ITripCandidateService.GetCandidates reads them directly off the criteria
    // it's given (same as TripQueryFilters.ToFilterCriteria() already does for the AI-driven
    // flow). Harmless for MapViewModel's own callers too, since
    // AirportFilterService.ApplyRunwayTypeAndPrefixFilters ignores those two fields.
    public AirportFilterCriteria BuildCriteria() => new()
    {
        MinRunwayFt = UseMeters ? GeoHelper.MetersToFeet(MinRunway) : MinRunway,
        MaxRunwayFt = UseMeters ? GeoHelper.MetersToFeet(MaxRunway) : MaxRunway,
        RequireInstrumentApproach = RequireInstrumentApproach,
        RequireAtis = RequireAtis,
        IcaoPrefixes = ParseIcaoPrefixes(IcaoPrefixes),
        FilterCenterIcao = string.IsNullOrWhiteSpace(FilterCenterIcao) ? null : FilterCenterIcao,
        FilterRadiusNm = FilterRadiusNm,
        ShowCivilAirports = ShowCivilAirports,
        ShowMilitaryAirports = ShowMilitaryAirports,
        ShowHeliportAirports = ShowHeliportAirports,
        ShowPrivateAirports = ShowPrivateAirports,
        ShowOtherAirports = ShowOtherAirports,
        ShowUnknownAirports = ShowUnknownAirports,
        ShowUnclassifiedAirports = ShowUnclassifiedAirports,
    };

    private static IReadOnlyList<string> ParseIcaoPrefixes(string raw) =>
        raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
           .Where(s => s.Length > 0)
           .ToList();

    // Persists the current values without raising Changed — exposed so a caller that doesn't
    // have an equivalent "FiltersApplied" live-layer-refresh concept (e.g. TripPlanViewModel's
    // "Add Filtered Airports") can save without implying that semantics.
    public void Persist() => _save(ToFields());

    private AirportFilterFields ToFields() => new()
    {
        MinRunway = MinRunway,
        MaxRunway = MaxRunway,
        UseMeters = UseMeters,
        RequireInstrumentApproach = RequireInstrumentApproach,
        RequireAtis = RequireAtis,
        FilterCenterIcao = FilterCenterIcao,
        FilterRadiusNm = FilterRadiusNm,
        ShowVisited = ShowVisited,
        ShowNotVisited = ShowNotVisited,
        ShowCivilAirports = ShowCivilAirports,
        ShowMilitaryAirports = ShowMilitaryAirports,
        ShowHeliportAirports = ShowHeliportAirports,
        ShowPrivateAirports = ShowPrivateAirports,
        ShowOtherAirports = ShowOtherAirports,
        ShowUnknownAirports = ShowUnknownAirports,
        ShowUnclassifiedAirports = ShowUnclassifiedAirports,
        IcaoPrefixes = IcaoPrefixes,
    };

    private void OnApply()
    {
        _save(ToFields());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnClear()
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
        _save(ToFields());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
