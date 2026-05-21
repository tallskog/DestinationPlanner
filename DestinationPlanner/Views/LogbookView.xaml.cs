using System.Windows;
using System.Windows.Controls;
using DestinationPlanner.Models;

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
}
