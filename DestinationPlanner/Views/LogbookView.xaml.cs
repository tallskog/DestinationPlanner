using System.Windows;
using System.Windows.Controls;
using DestinationPlanner.Models;
using DestinationPlanner.ViewModels;

namespace DestinationPlanner.Views;

public partial class LogbookView : UserControl
{
    public LogbookView()
    {
        InitializeComponent();
    }

    private void RatingButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is FlightRecord flight)
        {
            var window = new LandingRatingDetailWindow(flight) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }
    }

    // Opening a Window is a View-layer concern, kept out of LogbookViewModel — same rationale
    // as TripPlanView.xaml.cs's ViewOnMap_Click/ViewCandidatesOnMap_Click. Reaches IAirportDataService
    // via the main window's MainViewModel rather than threading it into LogbookViewModel's
    // constructor, which has never needed it.
    private void ShowOnMap_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LogbookViewModel vm || vm.SelectedFlight is not { } flight) return;
        if (Window.GetWindow(this)?.DataContext is not MainViewModel mainVm) return;

        new FlightLegMapWindow(flight, mainVm.AirportData) { Owner = Window.GetWindow(this) }.Show();
    }
}
