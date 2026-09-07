using DestinationPlanner.Models;
using DestinationPlanner.Services;
using System.Windows;
using System.Windows.Input;

namespace DestinationPlanner.Views;

// Lists every saved airport override (US48) so an accidental "Hidden" or a stale correction —
// e.g. an airport MSFS later recognizes correctly — can be found and cleared or edited without
// having to relocate the airport on a map and right-click it again.
public partial class AirportOverridesWindow : Window
{
    private readonly IAirportDataService _airportData;
    private readonly IAirportOverrideService _overrideService;

    private sealed record OverrideRow(string Icao, string DisplayName, string Summary, Airport? Airport);

    public AirportOverridesWindow(IAirportDataService airportData, IAirportOverrideService overrideService)
    {
        InitializeComponent();
        _airportData = airportData;
        _overrideService = overrideService;
        Loaded += (_, _) => Refresh();

        // Keeps the list in sync with an edit/clear made from any map window's own right-click
        // Edit Airport, not just from this window's own Edit.../Clear Override actions.
        _overrideService.OverridesChanged += OnOverridesChanged;
        Closed += (_, _) => _overrideService.OverridesChanged -= OnOverridesChanged;
    }

    private void OnOverridesChanged(object? sender, EventArgs e) => Dispatcher.Invoke(Refresh);

    private void Refresh()
    {
        var rows = _overrideService.GetAll()
            .Select(kvp =>
            {
                var airport = _airportData.GetByIcao(kvp.Key);
                return new OverrideRow(
                    kvp.Key,
                    airport?.Name ?? "(airport data not currently loaded)",
                    BuildSummary(kvp.Value),
                    airport);
            })
            .OrderBy(r => r.Icao, StringComparer.OrdinalIgnoreCase)
            .ToList();

        OverridesGrid.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OverridesGrid.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string BuildSummary(AirportOverride o)
    {
        var parts = new List<string>();
        if (o.Type.HasValue) parts.Add($"Type: {o.Type}");
        if (o.Name is not null) parts.Add($"Name: \"{o.Name}\"");
        if (o.HasAtis.HasValue) parts.Add($"ATIS: {(o.HasAtis.Value ? "Yes" : "No")}");
        if (o.HasInstrumentApproach.HasValue) parts.Add($"ILS: {(o.HasInstrumentApproach.Value ? "Yes" : "No")}");
        if (o.LongestRunwayFt.HasValue) parts.Add($"Runway: {o.LongestRunwayFt.Value:N0} ft");
        if (o.IsHidden == true) parts.Add("Hidden");
        return parts.Count > 0 ? string.Join(", ", parts) : "(none)";
    }

    // This window is non-modal (opened via .Show()), so IsCancel/DialogResult-based closing
    // doesn't apply here — a plain Close() is needed instead.
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();
    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (OverridesGrid.SelectedItem is not OverrideRow row) return;

        if (row.Airport is null)
        {
            MessageBox.Show(
                $"{row.Icao} isn't in the currently loaded airport data, so it can't be edited right now.\n\n" +
                "You can still clear its override below.",
                "Airport not loaded", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // No explicit Refresh() here — OverridesChanged (subscribed in the constructor) handles it.
        new EditAirportDialog(row.Airport, _overrideService) { Owner = this }.ShowDialog();
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (OverridesGrid.SelectedItem is not OverrideRow row) return;

        if (row.Airport is not null)
            await _overrideService.SaveOverrideAsync(row.Airport, null);
        else
            await _overrideService.RemoveOverrideAsync(row.Icao);
    }
}
