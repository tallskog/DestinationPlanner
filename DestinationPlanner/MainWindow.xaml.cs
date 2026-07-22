using DestinationPlanner.Helpers;
using DestinationPlanner.Services;
using DestinationPlanner.ViewModels;
using DestinationPlanner.Views;
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

    public MainWindow(string logbookPath, AppSettings settings)
    {
        InitializeComponent();
        DataContext = new MainViewModel(logbookPath, settings);
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
            await TryApplyCachedNavigraphTypesAsync(vm);
            vm.Map.NotifyAirportDataLoaded();
        }
        catch { /* silently skip if cached files are corrupt – user can reload manually */ }
    }

    // US34 – silently re-apply the most recently downloaded Navigraph airport-type
    // data (if any) without requiring re-authentication. No-op if none has ever been synced.
    private static async Task TryApplyCachedNavigraphTypesAsync(MainViewModel vm)
    {
        try
        {
            string navigraphDir = Path.Combine(AppDataHelper.AppDataPath, "navigraph");
            if (!Directory.Exists(navigraphDir)) return;

            string? latest = Directory.GetFiles(navigraphDir, "*.3sdb")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (latest is null) return;

            var types = await Task.Run(() => vm.NavigraphData.ParseAirportTypes(latest));
            vm.NavigraphSession.LastAppliedTypesByIcao = types;
            vm.AirportData.ApplyAirportTypes(types);
        }
        catch { /* silently skip if cached Navigraph data is missing/corrupt */ }
    }

    // US34 – LoadAsync rebuilds the airport dictionary from scratch, which would
    // otherwise wipe out any Navigraph classification applied earlier this session.
    private static void ReapplyNavigraphTypes(MainViewModel vm)
    {
        if (vm.NavigraphSession.LastAppliedTypesByIcao is { } types)
            vm.AirportData.ApplyAirportTypes(types);
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
            ReapplyNavigraphTypes(vm);
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

    private async void DownloadAirportData_Click(object sender, RoutedEventArgs e)
    {
        const string baseUrl = "https://raw.githubusercontent.com/davidmegginson/ourairports-data/main/";
        var files = new[]
        {
            ("airports.csv",            true),
            ("runways.csv",             false),
            ("airport-frequencies.csv", false),
        };

        IsEnabled = false;
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            foreach (var (filename, required) in files)
            {
                string dest = Path.Combine(AppDataHelper.AppDataPath, filename);
                try
                {
                    var bytes = await http.GetByteArrayAsync(baseUrl + filename);
                    await File.WriteAllBytesAsync(dest, bytes);
                }
                catch when (!required)
                {
                    // optional file — skip silently
                }
            }

            string airportsCsv     = Path.Combine(AppDataHelper.AppDataPath, "airports.csv");
            string? runwaysCsv     = NullIfMissing(Path.Combine(AppDataHelper.AppDataPath, "runways.csv"));
            string? frequenciesCsv = NullIfMissing(Path.Combine(AppDataHelper.AppDataPath, "airport-frequencies.csv"));

            var vm = (MainViewModel)DataContext;
            await vm.AirportData.LoadAsync(airportsCsv, runwaysCsv, frequenciesCsv);
            ReapplyNavigraphTypes(vm);
            vm.Map.NotifyAirportDataLoaded();

            MessageBox.Show("Airport data downloaded and loaded successfully.", "Download complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    // US34 – downloads Navigraph DFD v2 data and applies airport_type (ARINC 424 5.177)
    // classification to the loaded airports. Signs in via device-flow if needed.
    private async void UpdateNavigraphAirportTypes_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (!vm.NavigraphAuth.IsConfigured)
        {
            MessageBox.Show("Navigraph integration is not configured on this build.", "Navigraph",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsEnabled = false;
        try
        {
            string? accessToken = await EnsureNavigraphAccessTokenAsync(vm);
            if (accessToken is null) return; // user cancelled/denied sign-in

            string sqlitePath = await vm.NavigraphData.DownloadCurrentPackageAsync(accessToken, CancellationToken.None);
            var types = await Task.Run(() => vm.NavigraphData.ParseAirportTypes(sqlitePath));

            vm.NavigraphSession.LastAppliedTypesByIcao = types;
            vm.AirportData.ApplyAirportTypes(types);
            vm.Map.NotifyAirportDataLoaded();

            MessageBox.Show($"Applied airport type data for {types.Count:N0} airports.", "Navigraph",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Navigraph sync failed:\n{ex.Message}", "Navigraph",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    // Reuses a still-valid in-memory access token, else refreshes the stored refresh
    // token, else falls back to the device-flow sign-in dialog. Returns null if the
    // user cancelled or was denied during sign-in (refresh failures always fall through
    // to sign-in rather than surfacing an error, per US34).
    private async Task<string?> EnsureNavigraphAccessTokenAsync(MainViewModel vm)
    {
        if (vm.NavigraphSession.HasValidAccessToken)
            return vm.NavigraphSession.AccessToken;

        string? refreshToken = NavigraphTokenStore.TryLoad(vm.Settings);
        if (refreshToken != null)
        {
            try
            {
                var refreshed = await vm.NavigraphAuth.RefreshAsync(refreshToken, CancellationToken.None);
                NavigraphTokenStore.Save(vm.Settings, refreshed.RefreshToken);
                vm.NavigraphSession.AccessToken = refreshed.AccessToken;
                vm.NavigraphSession.AccessTokenExpiresAtUtc = refreshed.AccessTokenExpiresAtUtc;
                return refreshed.AccessToken;
            }
            catch
            {
                NavigraphTokenStore.Clear(vm.Settings); // dead token — fall through to sign-in
            }
        }

        var signInVm = new NavigraphSignInViewModel(vm.NavigraphAuth);
        var dialog = new NavigraphSignInDialog(signInVm) { Owner = this };
        bool? result = dialog.ShowDialog();
        if (result != true || signInVm.Result is null) return null;

        NavigraphTokenStore.Save(vm.Settings, signInVm.Result.RefreshToken);
        vm.NavigraphSession.AccessToken = signInVm.Result.AccessToken;
        vm.NavigraphSession.AccessTokenExpiresAtUtc = signInVm.Result.AccessTokenExpiresAtUtc;
        return signInVm.Result.AccessToken;
    }

    private void NavigraphSignOut_Click(object sender, RoutedEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        NavigraphTokenStore.Clear(vm.Settings);
        vm.NavigraphSession.AccessToken = null;
        vm.NavigraphSession.AccessTokenExpiresAtUtc = null;
        MessageBox.Show("Signed out of Navigraph.", "Navigraph", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private static string? NullIfMissing(string path) => File.Exists(path) ? path : null;
}
