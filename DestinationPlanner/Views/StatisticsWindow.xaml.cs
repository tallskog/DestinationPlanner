using System.Windows;
using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Views;

public partial class StatisticsWindow : Window
{
    public StatisticsWindow(IReadOnlyList<FlightRecord> flights, IAirportDataService airportData)
    {
        InitializeComponent();

        var stats = LogbookStatisticsService.Calculate(flights, airportData);

        if (stats.TotalFlights == 0)
        {
            EmptyStateText.Visibility = Visibility.Visible;
            ContentScroll.Visibility = Visibility.Collapsed;
            return;
        }

        Populate(stats);
    }

    private void Populate(LogbookStatistics stats)
    {
        TotalFlightsText.Text = $"Total flights logged: {stats.TotalFlights}";
        VisitedAirportsText.Text = $"Airports visited: {stats.VisitedAirportCount}";
        SpanText.Text = $"Logbook span: {FormatDate(stats.FirstFlightDate)} – {FormatDate(stats.LastFlightDate)}";

        TopVisitedList.ItemsSource = stats.TopVisitedAirports.Select(a => $"{a.Icao} — {a.Count} visit{Plural(a.Count)}").ToList();
        TopLandedList.ItemsSource = stats.TopLandedAirports.Select(a => $"{a.Icao} — {a.Count} landing{Plural(a.Count)}").ToList();
        TopDepartedList.ItemsSource = stats.TopDepartedAirports.Select(a => $"{a.Icao} — {a.Count} departure{Plural(a.Count)}").ToList();
        TopRoutesList.ItemsSource = stats.TopRoutes.Select(r => $"{r.DepartureIcao} → {r.ArrivalIcao} — {r.Count} flight{Plural(r.Count)}").ToList();

        LongestByDistanceText.Text = stats.LongestLegByDistance is { } byDistance
            ? $"Longest leg (distance): {byDistance.Flight.DepartureIcao} → {byDistance.Flight.ArrivalIcao} — {byDistance.DistanceNm:F0} nm"
            : "Longest leg (distance): unavailable (airport data missing)";
        LongestByTimeText.Text = stats.LongestLegByTime is { } byTime
            ? $"Longest leg (time): {byTime.DepartureIcao} → {byTime.ArrivalIcao} — {FormatTime(byTime.BlockTime)}"
            : "Longest leg (time): —";

        AverageDistanceText.Text = stats.AverageLegDistanceNm is { } avgDist
            ? $"Average leg length: {avgDist:F0} nm"
            : "Average leg length: unavailable (airport data missing)";
        AverageTimeText.Text = stats.AverageLegTime is { } avgTime
            ? $"Average leg time: {FormatTime(avgTime)}"
            : "Average leg time: —";

        TotalDistanceText.Text = stats.TotalDistanceNm is { } totalDist
            ? $"Total distance: {totalDist:F0} nm"
            : "Total distance: unavailable (airport data missing)";
        TotalTimeText.Text = $"Total time: {FormatTime(stats.TotalTime)}";

        AircraftList.ItemsSource = stats.AircraftTypes
            .Select(a => $"{a.Model} — {a.LegCount} leg{Plural(a.LegCount)}, {FormatTime(a.TotalTime)}")
            .ToList();
    }

    private static string FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd") ?? "—";

    private static string FormatTime(TimeSpan ts) => $"{(int)ts.TotalHours}h {ts.Minutes}m";

    private static string Plural(int count) => count == 1 ? "" : "s";
}
