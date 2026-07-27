using DestinationPlanner.Models;
using DestinationPlanner.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DestinationPlanner.Views;

public partial class TripPlanView : UserControl
{
    // Tracks the most recently opened TripMapWindow (and the plan it's showing) so the Legs
    // grid's selection can be forwarded to it live. Only one window is kept in sync — if the
    // user opens several map windows, only the latest one tracks further selection changes.
    private TripMapWindow? _openMapWindow;
    private TripPlan? _openMapWindowPlan;

    public TripPlanView()
    {
        InitializeComponent();
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
