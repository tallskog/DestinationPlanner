using DestinationPlanner.Helpers;
using DestinationPlanner.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace DestinationPlanner;

public partial class MainWindow : Window
{
    private DispatcherTimer? _reconnectTimer;

    private readonly UpdateManager _updateManager = new(
        new GithubSource("https://github.com/tallskog/DestinationPlanner", null, false));
    private UpdateInfo? _pendingUpdate;

    public MainWindow(string logbookPath)
    {
        InitializeComponent();
        DataContext = new MainViewModel(logbookPath);
        Loaded += MainWindow_Loaded;
    }

    // Silently checks for a new release and downloads it if available.
    // Shows the status-bar badge when the download is complete.
    private async Task CheckForUpdatesInBackground()
    {
        try
        {
            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            if (_pendingUpdate == null) return;
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate);
            UpdateBadge.Visibility = Visibility.Visible;
        }
        catch { /* update failures are non-fatal */ }
    }

    private void UpdateBadge_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdate == null) return;
        _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var update = await _updateManager.CheckForUpdatesAsync();
            if (update == null)
            {
                MessageBox.Show("You are running the latest version.", "Check for Updates",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Version {update.TargetFullRelease.Version} is available. Download and restart now?",
                "Update available", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await _updateManager.DownloadUpdatesAsync(update);
            _updateManager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update check failed:\n{ex.Message}", "Check for Updates",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // US13 – auto-load airport data from AppData on startup if files are present
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _ = CheckForUpdatesInBackground();

        string airportsCsv = Path.Combine(AppDataHelper.AppDataPath, "airports.csv");
        if (!File.Exists(airportsCsv)) return;

        string? runwaysCsv     = NullIfMissing(Path.Combine(AppDataHelper.AppDataPath, "runways.csv"));
        string? frequenciesCsv = NullIfMissing(Path.Combine(AppDataHelper.AppDataPath, "airport-frequencies.csv"));

        var vm = (MainViewModel)DataContext;
        try
        {
            await vm.AirportData.LoadAsync(airportsCsv, runwaysCsv, frequenciesCsv);
            vm.Map.NotifyAirportDataLoaded();
        }
        catch { /* silently skip if cached files are corrupt – user can reload manually */ }
    }

    // OnSourceInitialized fires once the Win32 window handle (HWND) exists —
    // the earliest point at which SimConnect can be connected.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var vm   = (MainViewModel)DataContext;
        var hwnd = new WindowInteropHelper(this).Handle;

        vm.SimConnect.Connect(hwnd);

        // Retry connection every 10 s so the app auto-connects if MSFS starts later.
        _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _reconnectTimer.Tick += (_, _) =>
        {
            if (!vm.SimConnect.IsConnected)
                vm.SimConnect.Connect(hwnd);
        };
        _reconnectTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _reconnectTimer?.Stop();
        ((MainViewModel)DataContext).SimConnect.Disconnect();
        base.OnClosed(e);
    }

    // US13 – copy airport CSV files to AppData, then load from there
    private async void LoadAirportData_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select airports.csv (OurAirports)",
            Filter = "airports.csv|airports.csv|CSV files (*.csv)|*.csv",
        };
        if (dlg.ShowDialog() != true) return;

        string srcAirports = dlg.FileName;
        string srcDir      = Path.GetDirectoryName(srcAirports)!;

        string destAirports = Path.Combine(AppDataHelper.AppDataPath, "airports.csv");
        File.Copy(srcAirports, destAirports, overwrite: true);

        string? srcRunways      = NullIfMissing(Path.Combine(srcDir, "runways.csv"));
        string? srcFrequencies  = NullIfMissing(Path.Combine(srcDir, "airport-frequencies.csv"));

        string? destRunways     = null;
        string? destFrequencies = null;

        if (srcRunways != null)
        {
            destRunways = Path.Combine(AppDataHelper.AppDataPath, "runways.csv");
            File.Copy(srcRunways, destRunways, overwrite: true);
        }
        if (srcFrequencies != null)
        {
            destFrequencies = Path.Combine(AppDataHelper.AppDataPath, "airport-frequencies.csv");
            File.Copy(srcFrequencies, destFrequencies, overwrite: true);
        }

        var vm = (MainViewModel)DataContext;
        try
        {
            await vm.AirportData.LoadAsync(destAirports, destRunways, destFrequencies);
            vm.Map.NotifyAirportDataLoaded();

            var missing = new List<string>();
            if (destRunways is null)     missing.Add("runways.csv (runway length filter will not work)");
            if (destFrequencies is null) missing.Add("airport-frequencies.csv (ATIS filter will not work)");
            if (missing.Count > 0)
                MessageBox.Show($"airports.csv loaded.\nThe following optional files were not found in the same folder:\n• {string.Join("\n• ", missing)}",
                                "Airport data loaded", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load airport data:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private static string? NullIfMissing(string path) => File.Exists(path) ? path : null;
}
