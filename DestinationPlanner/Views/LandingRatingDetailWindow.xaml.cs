using System.Windows;
using System.Windows.Media;
using DestinationPlanner.Helpers;
using DestinationPlanner.Models;

namespace DestinationPlanner.Views;

public partial class LandingRatingDetailWindow : Window
{
    private static readonly SolidColorBrush GreenBrush  = new(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush OrangeBrush = new(Color.FromRgb(0xE6, 0x51, 0x00));
    private static readonly SolidColorBrush RedBrush    = new(Color.FromRgb(0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush GrayBrush   = new(Colors.Gray);

    public LandingRatingDetailWindow(FlightRecord flight)
    {
        InitializeComponent();
        Title = $"Landing Rating — {flight.DepartureIcao} → {flight.ArrivalIcao} — {flight.Date:yyyy-MM-dd}";

        var breakdown = LandingRatingHelper.ComputeBreakdown(flight);
        if (breakdown is null)
        {
            OverallScoreText.Text = "Insufficient data to compute rating.";
            StarsText.Text        = "—";
            return;
        }

        MetricsGrid.ItemsSource = breakdown.Components
            .Select(c => new ComponentRow(
                $"{c.Name}  ({c.WeightDisplay})",
                c.MeasuredValue,
                c.ScoreDisplay,
                c.Score switch { null => GrayBrush, >= 80 => GreenBrush, >= 60 => OrangeBrush, _ => RedBrush }
            ))
            .ToList();

        OverallScoreText.Text = $"Overall score: {breakdown.TotalScore:F0} / 100";
        StarsText.Text        = new string('★', breakdown.Stars) + new string('☆', 5 - breakdown.Stars);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

internal record ComponentRow(string Name, string MeasuredValue, string ScoreDisplay, SolidColorBrush ScoreBrush);
