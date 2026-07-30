using DestinationPlanner.Models;
using DestinationPlanner.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DestinationPlanner.Views;

public partial class TripPlanView : UserControl
{
    // Tracks the most recently opened TripMapWindow (and the plan it's showing) so the Legs
    // grid's selection can be forwarded to it live. Only one window is kept in sync — if the
    // user opens several map windows, only the latest one tracks further selection changes.
    private TripMapWindow? _openMapWindow;
    private TripPlan? _openMapWindowPlan;
    private bool _searchWired;

    public TripPlanView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // Wires the candidate search box (US43) — mirrors MapView's airport search wiring exactly.
    // Deferred to Loaded (rather than the constructor) since DataContext isn't guaranteed to be
    // set yet when InitializeComponent runs.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_searchWired) return;
        _searchWired = true;

        CandidateSearchContainer.IsKeyboardFocusWithinChanged += (_, _) => UpdateCandidateSearchDropdownVisibility();
        CandidateSearchResultsList.MouseLeftButtonUp += (_, _) =>
        {
            if (DataContext is TripPlanViewModel vm && CandidateSearchResultsList.SelectedItem is Airport a)
                vm.AddCandidateAirport(a);
        };
        CandidateSearchBox.KeyDown += (_, ev) =>
        {
            if (DataContext is not TripPlanViewModel vm) return;

            if (ev.Key == Key.Escape)
            {
                vm.CandidateSearchText = string.Empty;
                CandidateSearchBox.Focus();
            }
            else if (ev.Key == Key.Enter)
            {
                if (CandidateSearchResultsList.SelectedItem is Airport sel) vm.AddCandidateAirport(sel);
                else if (vm.CandidateSearchResults.Count > 0) vm.AddCandidateAirport(vm.CandidateSearchResults[0]);
            }
            else if (ev.Key == Key.Down && CandidateSearchResultsList.HasItems)
            {
                CandidateSearchResultsList.SelectedIndex = 0;
                CandidateSearchResultsList.Focus();
                (CandidateSearchResultsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            }
        };
        CandidateSearchResultsList.KeyDown += (_, ev) =>
        {
            if (DataContext is not TripPlanViewModel vm) return;

            if (ev.Key == Key.Enter && CandidateSearchResultsList.SelectedItem is Airport a)
                vm.AddCandidateAirport(a);
            else if (ev.Key == Key.Escape)
            {
                vm.CandidateSearchText = string.Empty;
                CandidateSearchBox.Focus();
            }
        };

        if (DataContext is TripPlanViewModel initialVm)
        {
            initialVm.PropertyChanged += (_, ev) =>
            {
                if (ev.PropertyName == nameof(TripPlanViewModel.CandidateSearchResults))
                    Dispatcher.Invoke(UpdateCandidateSearchDropdownVisibility);
            };
        }
    }

    private void UpdateCandidateSearchDropdownVisibility()
    {
        var vm = DataContext as TripPlanViewModel;
        CandidateSearchDropdown.Visibility =
            (vm?.CandidateSearchResults.Count > 0 && CandidateSearchContainer.IsKeyboardFocusWithin)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void ViewCandidatesOnMap_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TripPlanViewModel vm || vm.Candidates.Count == 0) return;

        new CandidateMapWindow(vm.Candidates.ToList()) { Owner = Window.GetWindow(this) }.Show();
    }

    // Opening a Window is a View-layer concern — kept out of TripPlanViewModel so it stays
    // WPF/Mapsui-agnostic (consistent with how OpenAipApiKeyDialog etc. are opened from
    // code-behind rather than from a ViewModel command).
    private void ViewOnMap_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TripPlanViewModel vm || vm.SelectedPlan is null) return;

        var window = new TripMapWindow(vm.SelectedPlan, vm.AirportData) { Owner = Window.GetWindow(this) };
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openMapWindow, window)) { _openMapWindow = null; _openMapWindowPlan = null; }
        };
        // TripMapWindow builds its route lines on its own Loaded handler (registered in its
        // constructor, which runs before this one is attached) — ours runs after, so the lines
        // already exist by the time we apply the current selection.
        window.Loaded += (_, _) => window.HighlightLegs(GetSelectedLegOrders());

        _openMapWindow = window;
        _openMapWindowPlan = vm.SelectedPlan;
        window.Show();
    }

    private void LegsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not TripPlanViewModel vm) return;
        if (_openMapWindow is null || !ReferenceEquals(_openMapWindowPlan, vm.SelectedPlan)) return;

        _openMapWindow.HighlightLegs(GetSelectedLegOrders());
    }

    private HashSet<int> GetSelectedLegOrders() =>
        LegsGrid.SelectedItems.Cast<TripLegRow>().Select(r => r.Order).ToHashSet();
}
