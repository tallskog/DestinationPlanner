using DestinationPlanner.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace DestinationPlanner;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void LoadAirportData_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select airports.csv (OurAirports)",
            Filter = "airports.csv|airports.csv|CSV files (*.csv)|*.csv",
        };
        if (dlg.ShowDialog() != true) return;

        string airportsCsv = dlg.FileName;
        string? runwaysCsv = null;

        // Auto-detect runways.csv next to airports.csv
        string sibling = Path.Combine(Path.GetDirectoryName(airportsCsv)!, "runways.csv");
        if (File.Exists(sibling))
            runwaysCsv = sibling;

        var vm = (MainViewModel)DataContext;
        try
        {
            await vm.AirportData.LoadAsync(airportsCsv, runwaysCsv);
            vm.Map.NotifyAirportDataLoaded();
            if (runwaysCsv is null)
                MessageBox.Show("airports.csv loaded.\nNo runways.csv found in the same folder — runway length filter will not work.",
                                "Airport data loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load airport data:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
