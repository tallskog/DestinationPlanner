using DestinationPlanner.Models;
using DestinationPlanner.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace DestinationPlanner.Views;

// Right-click "Edit Airport" dialog (all four map surfaces — see CLAUDE.md's "Map info box
// parity" note, extended here since this action is now shared by all of them too). Lets the
// user layer a manual correction on top of OurAirports/OpenAIP data without ever touching that
// underlying data — see Models/AirportOverride and Services/AirportOverrideService. Saved
// immediately on OK; Cancel discards whatever was typed.
public partial class EditAirportDialog : Window
{
    private static readonly (string Label, AirportType? Value)[] TypeOptions =
    [
        ("(No override)", null),
        (nameof(AirportType.Civil), AirportType.Civil),
        (nameof(AirportType.Military), AirportType.Military),
        (nameof(AirportType.Heliport), AirportType.Heliport),
        (nameof(AirportType.Private), AirportType.Private),
        (nameof(AirportType.Other), AirportType.Other),
        (nameof(AirportType.Unknown), AirportType.Unknown),
        (nameof(AirportType.Unclassified), AirportType.Unclassified),
    ];

    private static readonly (string Label, bool? Value)[] TriStateOptions =
    [
        ("(No override)", null),
        ("Yes", true),
        ("No", false),
    ];

    private readonly Airport _airport;
    private readonly IAirportOverrideService _overrideService;

    public EditAirportDialog(Airport airport, IAirportOverrideService overrideService)
    {
        InitializeComponent();
        _airport = airport;
        _overrideService = overrideService;
        Title = $"Edit Airport — {airport.Icao}";

        HeaderText.Text =
            $"{airport.Icao} — {airport.Name}\n" +
            $"Detected: type={airport.Type}, ATIS={airport.HasAtis}, ILS={airport.HasInstrumentApproach}, " +
            $"longest runway={(airport.LongestRunwayFt > 0 ? $"{airport.LongestRunwayFt:N0} ft" : "N/A")}";

        TypeCombo.ItemsSource = TypeOptions.Select(o => o.Label).ToList();
        AtisCombo.ItemsSource = TriStateOptions.Select(o => o.Label).ToList();
        IlsCombo.ItemsSource = TriStateOptions.Select(o => o.Label).ToList();

        var o = airport.Override;
        TypeCombo.SelectedIndex = Array.FindIndex(TypeOptions, t => t.Value == o?.Type);
        AtisCombo.SelectedIndex = Array.FindIndex(TriStateOptions, t => t.Value == o?.HasAtis);
        IlsCombo.SelectedIndex  = Array.FindIndex(TriStateOptions, t => t.Value == o?.HasInstrumentApproach);
        NameBox.Text = o?.Name ?? string.Empty;
        RunwayFtBox.Text = o?.LongestRunwayFt?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        HiddenCheck.IsChecked = o?.IsHidden ?? false;

        Loaded += (_, _) => TypeCombo.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        int? runwayFt = null;
        if (!string.IsNullOrWhiteSpace(RunwayFtBox.Text))
        {
            if (!int.TryParse(RunwayFtBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
            {
                MessageBox.Show("Longest runway (ft) must be a positive whole number, or left blank.",
                    "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            runwayFt = parsed;
        }

        var value = new AirportOverride
        {
            Type = TypeOptions[TypeCombo.SelectedIndex].Value,
            Name = string.IsNullOrWhiteSpace(NameBox.Text) ? null : NameBox.Text.Trim(),
            HasAtis = TriStateOptions[AtisCombo.SelectedIndex].Value,
            HasInstrumentApproach = TriStateOptions[IlsCombo.SelectedIndex].Value,
            LongestRunwayFt = runwayFt,
            IsHidden = HiddenCheck.IsChecked == true ? true : null,
        };

        await _overrideService.SaveOverrideAsync(_airport, value.IsEmpty ? null : value);
        DialogResult = true;
    }
}
