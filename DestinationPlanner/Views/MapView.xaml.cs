using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.ViewModels;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiPen   = Mapsui.Styles.Pen;
using Mapsui.Tiling;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DestinationPlanner.Views;

public partial class MapView : UserControl
{
    private bool _initialized;
    private MapViewModel? _vm;
    private MemoryLayer? _airportLayer;
    private MemoryLayer? _logbookLayer;
    private MemoryLayer? _greenRingLayer;
    private MemoryLayer? _redRingLayer;
    private int _airportCount;
    private int _logbookCount;

    // Live aircraft position marker
    private TextBlock? _aircraftMarker;
    private RotateTransform? _aircraftRotate;
    private double _aircraftLat;
    private double _aircraftLon;

    private Airport? _primaryAirport;
    private Airport? _secondaryAirport;

    private CancellationTokenSource? _primaryMetarCts;
    private CancellationTokenSource? _secondaryMetarCts;

    private readonly IMetarService _metarService = new MetarService();

    // Precipitation radar overlay (US38) — a Mapsui tile layer toggled on/off, refreshed only
    // on explicit user request (no background polling; see requirements.md US38).
    private readonly IPrecipitationRadarService _precipitationRadarService = new PrecipitationRadarService();
    private Mapsui.Tiling.Layers.TileLayer? _precipitationLayer;
    private CancellationTokenSource? _precipitationCts;

    // WPF elements drawn on the Canvas overlay for the selection line
    private Line? _selectionLine;
    private TextBlock? _selectionDistLabel;

    // Amber rather than blue — a blue dot disappears against blue-toned rain radar
    // shading and ocean tiles; amber stays legible against both. Thicker outline (2f,
    // vs. the original 1f) so the dot still reads clearly under heavy precipitation.
    private static readonly ZoomCircleStyle AirportStyle = new(
        fill:         new MapsuiColor(255, 193, 7, 200),
        outline:      new MapsuiColor(140, 100, 0),
        outlineWidth: 2f);

    private static readonly ZoomCircleStyle LogbookStyle = new(
        fill:         new MapsuiColor(220, 100, 0, 230),
        outline:      new MapsuiColor(140, 50, 0),
        outlineWidth: 1.5f);

    // Green ring = departed; transparent fill so OSM shows through the gap between orange dot and ring.
    private static readonly ZoomCircleStyle GreenRingStyle = new(
        fill:            null,
        outline:         new MapsuiColor(40, 200, 60, 230),
        outlineWidth:    2.5f,
        scaleMultiplier: 1.5);

    // Red ring = landed; inner position when landed-only, outer when also departed (both rings visible).
    private static readonly RingStyle RedRingStyle = new(
        color:           new MapsuiColor(200, 40, 40, 230),
        outlineWidth:    2.5f,
        innerMultiplier: 1.5,
        outerMultiplier: 2.0);

    // Tracks whether each popup was open before the window was minimized
    private bool _primaryWasOpen;
    private bool _secondaryWasOpen;

    // Tracks whether each popup was open before a tab switch
    private bool _tabPrimaryWasOpen;
    private bool _tabSecondaryWasOpen;

    // Pre-built WPF brushes used in MakeRunwayTextBlock (avoids per-call allocation)
    private static readonly SolidColorBrush RunwayForeground =
        new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOMOVE     = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    public MapView()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void Attribution_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch { /* best-effort — attribution link opening failures are non-fatal */ }
        e.Handled = true;
    }

    // ---- Precipitation radar overlay ----

    private async void PrecipitationToggle_Checked(object sender, RoutedEventArgs e) => await LoadPrecipitationOverlayAsync();

    private void PrecipitationToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _precipitationCts?.Cancel();
        RemovePrecipitationLayer();
        PrecipitationRefreshButton.IsEnabled = false;
        PrecipitationStatusText.Text = string.Empty;
        PrecipitationAttributionText.Visibility = Visibility.Collapsed;
    }

    private async void PrecipitationRefresh_Click(object sender, RoutedEventArgs e) => await LoadPrecipitationOverlayAsync();

    private async Task LoadPrecipitationOverlayAsync()
    {
        _precipitationCts?.Cancel();
        var cts = new CancellationTokenSource();
        _precipitationCts = cts;

        PrecipitationToggleButton.IsChecked = true;
        PrecipitationRefreshButton.IsEnabled = false;
        PrecipitationStatusText.Text = "loading…";

        var frame = await _precipitationRadarService.GetLatestFrameAsync(cts.Token);

        if (cts.IsCancellationRequested) return;

        if (frame is null)
        {
            PrecipitationStatusText.Text = "unavailable";
            PrecipitationRefreshButton.IsEnabled = true;
            return;
        }

        // RainViewer's tiles top out at zoom 7 — deeper requests return a literal
        // "Zoom Level Not Supported" placeholder image instead of a 404. Capping the
        // schema here makes BruTile stretch (over-zoom) the deepest real tile instead
        // of requesting past it.
        var tileSource = new BruTile.Web.HttpTileSource(
            new BruTile.Predefined.GlobalSphericalMercator(0, 7),
            frame.TileUrlTemplate,
            name: "Precipitation Radar");

        RemovePrecipitationLayer();
        _precipitationLayer = new Mapsui.Tiling.Layers.TileLayer(tileSource) { Name = "Precipitation Radar" };
        // Insert just above the OSM base layer (index 0) so airport markers/rings/logbook
        // layers added afterwards still render on top of the radar, not underneath it.
        MapCtrl.Map.Layers.Insert(1, _precipitationLayer);

        PrecipitationStatusText.Text = frame.FrameTimeUtc.ToLocalTime().ToString("HH:mm");
        PrecipitationRefreshButton.IsEnabled = true;
        PrecipitationAttributionText.Visibility = Visibility.Visible;
    }

    private void RemovePrecipitationLayer()
    {
        if (_precipitationLayer is null) return;
        MapCtrl.Map.Layers.Remove(_precipitationLayer);
        _precipitationLayer = null;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tabPrimaryWasOpen   = PrimaryPopup.IsOpen;
        _tabSecondaryWasOpen = SecondaryPopup.IsOpen;
        PrimaryPopup.IsOpen   = false;
        SecondaryPopup.IsOpen = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            if (_tabPrimaryWasOpen && _primaryAirport != null)
            {
                SetPopupPosition(PrimaryPopup, _primaryAirport);
                PrimaryPopup.IsOpen = true;
            }
            if (_tabSecondaryWasOpen && _secondaryAirport != null)
            {
                SetPopupPosition(SecondaryPopup, _secondaryAirport);
                SecondaryPopup.IsOpen = true;
            }
            _tabPrimaryWasOpen   = false;
            _tabSecondaryWasOpen = false;
            return;
        }
        _initialized = true;

        _vm = DataContext as MapViewModel;
        if (_vm is null) return;

        _vm.FiltersApplied += (_, _) => Dispatcher.Invoke(() => { RefreshAirportLayer(); RefreshLogbookLayer(); });
        _vm.LogbookChanged += (_, _) => Dispatcher.Invoke(RefreshLogbookLayer);
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.AircraftMoved  += (_, e) => Dispatcher.Invoke(() => UpdateAircraftMarker(e));
        MapCtrl.Info += OnMapInfo;

        _airportLayer  = new MemoryLayer { Name = "Airports",      Style = AirportStyle  };
        _greenRingLayer = new MemoryLayer { Name = "DepartedRings", Style = GreenRingStyle };
        _redRingLayer   = new MemoryLayer { Name = "LandedRings",   Style = RedRingStyle  };
        _logbookLayer  = new MemoryLayer { Name = "Logbook",       Style = LogbookStyle  };

        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(_airportLayer);
        map.Layers.Add(_greenRingLayer);
        map.Layers.Add(_redRingLayer);
        map.Layers.Add(_logbookLayer);

        MapCtrl.Map = map;

        // Aircraft position marker — WPF overlay so it isn't part of Mapsui's layer system.
        _aircraftRotate = new RotateTransform();
        _aircraftMarker = new TextBlock
        {
            Text                  = "✈",
            FontSize              = 22,
            Foreground            = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 100, 220)),
            RenderTransform       = _aircraftRotate,
            RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            Visibility            = Visibility.Collapsed,
            IsHitTestVisible      = false,
        };
        SelectionOverlay.Children.Add(_aircraftMarker);

        map.Navigator.ViewportChanged += (_, _) => Dispatcher.Invoke(OnViewportChanged);

        var cx = GeoHelper.LonToMercatorX(15.0);
        var cy = GeoHelper.LatToMercatorY(50.0);
        map.ViewportInitialized += (_, _) =>
            map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), 2_500_000);

        PrimaryPopup.Opened   += (_, _) => RemovePopupTopmost(PrimaryPopup);
        SecondaryPopup.Opened += (_, _) => RemovePopupTopmost(SecondaryPopup);

        SearchContainer.IsKeyboardFocusWithinChanged += (_, _) => UpdateSearchDropdownVisibility();
        SearchResultsList.MouseLeftButtonUp += (_, _) =>
        {
            if (SearchResultsList.SelectedItem is Airport a) SelectSearchResult(a);
        };
        SearchBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (_vm != null) _vm.SearchText = string.Empty;
                SearchBox.Focus();
            }
            else if (e.Key == Key.Down && SearchResultsList.HasItems)
            {
                SearchResultsList.SelectedIndex = 0;
                SearchResultsList.Focus();
                (SearchResultsList.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.Controls.ListBoxItem)?.Focus();
            }
        };
        SearchResultsList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && SearchResultsList.SelectedItem is Airport a)
                SelectSearchResult(a);
            else if (e.Key == Key.Escape)
            {
                if (_vm != null) _vm.SearchText = string.Empty;
                SearchBox.Focus();
            }
        };

        var mainWindow = Window.GetWindow(this);
        if (mainWindow != null)
        {
            mainWindow.StateChanged    += OnMainWindowStateChanged;
            mainWindow.LocationChanged += OnMainWindowLocationChanged;
        }
    }

    private static void RemovePopupTopmost(System.Windows.Controls.Primitives.Popup popup)
    {
        if (PresentationSource.FromVisual(popup.Child) is HwndSource src)
            SetWindowPos(src.Handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        var window = (Window)sender!;
        if (window.WindowState == WindowState.Minimized)
        {
            _primaryWasOpen   = PrimaryPopup.IsOpen;
            _secondaryWasOpen = SecondaryPopup.IsOpen;
            PrimaryPopup.IsOpen   = false;
            SecondaryPopup.IsOpen = false;
        }
        else
        {
            if (_primaryWasOpen)   PrimaryPopup.IsOpen   = true;
            if (_secondaryWasOpen) SecondaryPopup.IsOpen = true;
            _primaryWasOpen   = false;
            _secondaryWasOpen = false;
        }
    }

    private void OnMainWindowLocationChanged(object? sender, EventArgs e)
    {
        NudgePopup(PrimaryPopup);
        NudgePopup(SecondaryPopup);
    }

    // Forces WPF to recalculate popup screen position after the host window moves.
    // AllowsTransparency popups don't reposition automatically on window move.
    private static void NudgePopup(System.Windows.Controls.Primitives.Popup popup)
    {
        if (!popup.IsOpen) return;
        var h = popup.HorizontalOffset;
        popup.HorizontalOffset = h + 1;
        popup.HorizontalOffset = h;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.FilterCenterIcao) or nameof(MapViewModel.FilterRadiusNm))
            Dispatcher.Invoke(RefreshAirportLayer);
        else if (e.PropertyName == nameof(MapViewModel.SearchResults))
            Dispatcher.Invoke(UpdateSearchDropdownVisibility);
    }

    // ---- Layer refresh ----

    private void RefreshLogbookLayer()
    {
        if (_vm is null || _logbookLayer is null) return;
        var airports = _vm.GetLogbookAirports();
        _logbookLayer.Features = airports.Select(MakeFeature).ToList();
        _logbookLayer.DataHasChanged();
        _logbookCount = airports.Count;
        RefreshRingLayers();
        UpdateStatus();
    }

    private void RefreshRingLayers()
    {
        if (_vm is null || _greenRingLayer is null || _redRingLayer is null) return;

        var departed = _vm.GetDepartedAirports();
        var landed   = _vm.GetLandedAirports();
        var departedIcaos = departed.Select(a => a.Icao).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _greenRingLayer.Features = departed.Select(MakeFeature).ToList();

        _redRingLayer.Features = landed.Select(a =>
        {
            var f = MakeFeature(a);
            if (departedIcaos.Contains(a.Icao))
                f["ring_outer"] = true;
            return f;
        }).ToList();

        _greenRingLayer.DataHasChanged();
        _redRingLayer.DataHasChanged();
    }

    private void RefreshAirportLayer()
    {
        if (_vm is null || _airportLayer is null) return;
        var airports = _vm.GetAllFilteredAirports();
        _airportLayer.Features = airports.Select(MakeFeature).ToList();
        _airportLayer.DataHasChanged();
        _airportCount = airports.Count;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_airportCount == 0 && _logbookCount == 0)
        {
            MapStatus.Text = string.Empty;
            return;
        }
        var parts = new List<string>();
        if (_airportCount > 0) parts.Add($"{_airportCount:N0} airports");
        if (_logbookCount > 0) parts.Add($"{_logbookCount:N0} visited");
        MapStatus.Text = string.Join(" · ", parts);
    }

    // ---- Click handling ----

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (_airportLayer is null || _logbookLayer is null || _greenRingLayer is null || _redRingLayer is null) return;

        var feature = e.GetMapInfo(new[] { _logbookLayer, _greenRingLayer, _redRingLayer, _airportLayer })?.Feature;

        if (feature is null)
        {
            ClosePrimaryPopup();
            CloseSecondaryPopup();
            _primaryAirport = null;
            _secondaryAirport = null;
            UpdateSelectionLine();
            return;
        }

        var icao = feature["icao"] as string ?? string.Empty;
        var airport = _vm?.GetAirportByIcao(icao);
        if (airport is null) return;

        bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

        if (isCtrl && _primaryAirport != null)
        {
            _secondaryAirport = airport;
            OpenPopup(airport, isPrimary: false);
        }
        else
        {
            // Plain left-click (or ctrl with no primary): set primary, clear secondary
            _primaryAirport = airport;
            _secondaryAirport = null;
            CloseSecondaryPopup();
            OpenPopup(airport, isPrimary: true);
        }

        UpdateSelectionLine();
    }

    // ---- Popup management ----

    private void OpenPopup(Airport airport, bool isPrimary)
    {
        if (isPrimary)
        {
            _primaryMetarCts?.Cancel();
            _primaryMetarCts = new CancellationTokenSource();
        }
        else
        {
            _secondaryMetarCts?.Cancel();
            _secondaryMetarCts = new CancellationTokenSource();
        }

        var icaoBlock   = isPrimary ? PrimaryIcao   : SecondaryIcao;
        var nameBlock   = isPrimary ? PrimaryName   : SecondaryName;
        var runwaysList = isPrimary ? PrimaryRunwaysList : SecondaryRunwaysList;
        var metarBlock  = isPrimary ? PrimaryMetar  : SecondaryMetar;

        icaoBlock.Text = airport.Icao;
        nameBlock.Text = airport.Name;

        runwaysList.Children.Clear();
        if (airport.Runways.Count > 0)
        {
            foreach (var rwy in airport.Runways)
                runwaysList.Children.Add(MakeRunwayLine($"  {rwy.Ident}: {rwy.LengthFt:N0} ft"));
        }
        else if (airport.LongestRunwayFt > 0)
        {
            runwaysList.Children.Add(MakeRunwayLine($"  {airport.LongestRunwayFt:N0} ft"));
        }
        else
        {
            runwaysList.Children.Add(MakeRunwayLine("  N/A"));
        }

        metarBlock.Text = "METAR: Loading…";

        var popup = isPrimary ? PrimaryPopup : SecondaryPopup;
        SetPopupPosition(popup, airport);
        popup.IsOpen = true;

        var cts = isPrimary ? _primaryMetarCts! : _secondaryMetarCts!;
        _ = LoadMetarAsync(airport.Icao, metarBlock, cts.Token);
    }

    private static TextBlock MakeRunwayLine(string text) =>
        new() { Text = text, FontSize = 11, Foreground = RunwayForeground };

    private async Task LoadMetarAsync(string icao, TextBlock target, CancellationToken token)
    {
        var metar = await _metarService.FetchMetarAsync(icao, token);
        if (token.IsCancellationRequested) return;

        await Dispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            target.Text = metar is not null ? $"METAR: {metar}" : "METAR: Not available";
        });
    }

    private void ClosePrimaryPopup()
    {
        _primaryMetarCts?.Cancel();
        PrimaryPopup.IsOpen = false;
    }

    private void CloseSecondaryPopup()
    {
        _secondaryMetarCts?.Cancel();
        SecondaryPopup.IsOpen = false;
    }

    // ---- Popup positioning ----

    private void OnViewportChanged()
    {
        if (_primaryAirport  != null && PrimaryPopup.IsOpen)   SetPopupPosition(PrimaryPopup,   _primaryAirport);
        if (_secondaryAirport != null && SecondaryPopup.IsOpen) SetPopupPosition(SecondaryPopup, _secondaryAirport);
        UpdateSelectionLinePositions();
        RepositionAircraftMarker();
    }

    private void SetPopupPosition(System.Windows.Controls.Primitives.Popup popup, Airport airport)
    {
        var (sx, sy) = MercatorToScreen(
            GeoHelper.LonToMercatorX(airport.Longitude),
            GeoHelper.LatToMercatorY(airport.Latitude));
        popup.HorizontalOffset = sx + 10;
        popup.VerticalOffset   = sy + 10;
    }

    // Converts Mercator world coords to MapCtrl-relative screen pixel coords (no rotation).
    private (double x, double y) MercatorToScreen(double mercX, double mercY)
    {
        var vp = MapCtrl.Map.Navigator.Viewport;
        var sx = (mercX - vp.CenterX) / vp.Resolution + vp.Width  / 2.0;
        var sy = (mercY - vp.CenterY) / vp.Resolution * -1.0 + vp.Height / 2.0;
        return (sx, sy);
    }

    // ---- Selection line (WPF Canvas overlay) ----

    private void UpdateSelectionLine()
    {
        if (_selectionLine != null) SelectionOverlay.Children.Remove(_selectionLine);
        if (_selectionDistLabel != null) SelectionOverlay.Children.Remove(_selectionDistLabel);
        _selectionLine = null;
        _selectionDistLabel = null;

        if (_primaryAirport is null || _secondaryAirport is null) return;

        var distNm = GeoHelper.DistanceNm(
            _primaryAirport.Latitude,    _primaryAirport.Longitude,
            _secondaryAirport.Latitude,  _secondaryAirport.Longitude);

        _selectionLine = new Line
        {
            Stroke          = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 180, 80, 0)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 3 }
        };

        _selectionDistLabel = new TextBlock
        {
            Text       = $"{distNm:N0} nm",
            FontSize   = 11,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 40, 0)),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 240, 200)),
            Padding    = new Thickness(3, 1, 3, 1)
        };

        SelectionOverlay.Children.Add(_selectionLine);
        SelectionOverlay.Children.Add(_selectionDistLabel);

        UpdateSelectionLinePositions();
    }

    private void UpdateSelectionLinePositions()
    {
        if (_selectionLine is null || _selectionDistLabel is null ||
            _primaryAirport is null || _secondaryAirport is null)
            return;

        var (x1, y1) = MercatorToScreen(
            GeoHelper.LonToMercatorX(_primaryAirport.Longitude),
            GeoHelper.LatToMercatorY(_primaryAirport.Latitude));
        var (x2, y2) = MercatorToScreen(
            GeoHelper.LonToMercatorX(_secondaryAirport.Longitude),
            GeoHelper.LatToMercatorY(_secondaryAirport.Latitude));

        _selectionLine.X1 = x1;
        _selectionLine.Y1 = y1;
        _selectionLine.X2 = x2;
        _selectionLine.Y2 = y2;

        Canvas.SetLeft(_selectionDistLabel, (x1 + x2) / 2 - 22);
        Canvas.SetTop(_selectionDistLabel,  (y1 + y2) / 2 - 10);
    }

    // ---- Search ----

    private void UpdateSearchDropdownVisibility()
    {
        SearchDropdown.Visibility =
            (_vm?.SearchResults.Count > 0 && SearchContainer.IsKeyboardFocusWithin)
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void SelectSearchResult(Airport airport)
    {
        SearchResultsList.SelectedItem = null;
        if (_vm != null) _vm.SearchText = string.Empty;

        _primaryAirport   = airport;
        _secondaryAirport = null;
        CloseSecondaryPopup();
        UpdateSelectionLine();

        var cx = GeoHelper.LonToMercatorX(airport.Longitude);
        var cy = GeoHelper.LatToMercatorY(airport.Latitude);
        MapCtrl.Map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), 1_700);

        OpenPopup(airport, isPrimary: true);
        SearchBox.Focus();
    }

    // ---- Aircraft marker ----

    private void UpdateAircraftMarker(AircraftPositionEventArgs e)
    {
        if (_aircraftMarker is null || _aircraftRotate is null || _vm is null) return;

        _aircraftLat = e.Latitude;
        _aircraftLon = e.Longitude;
        // ✈ (U+2708) renders pointing right (East) in most fonts.
        // Subtract 45° because the ✈ glyph naturally points northeast (45° from north).
        _aircraftRotate.Angle = e.HeadingDegrees - 45;

        _aircraftMarker.Visibility = _vm.SimConnected ? Visibility.Visible : Visibility.Collapsed;
        RepositionAircraftMarker();
    }

    private void RepositionAircraftMarker()
    {
        if (_aircraftMarker is null || _aircraftMarker.Visibility != Visibility.Visible) return;

        var (sx, sy) = MercatorToScreen(
            GeoHelper.LonToMercatorX(_aircraftLon),
            GeoHelper.LatToMercatorY(_aircraftLat));
        Canvas.SetLeft(_aircraftMarker, sx - 11);
        Canvas.SetTop(_aircraftMarker,  sy - 11);
    }

    // ---- Feature factory ----

    private static PointFeature MakeFeature(Airport airport)
    {
        var x = GeoHelper.LonToMercatorX(airport.Longitude);
        var y = GeoHelper.LatToMercatorY(airport.Latitude);
        var feature = new PointFeature(new MPoint(x, y));
        feature["icao"]      = airport.Icao;
        feature["name"]      = airport.Name;
        feature["runway_ft"] = airport.LongestRunwayFt;
        feature["ils"]       = airport.HasInstrumentApproach;
        return feature;
    }

    // ---- Zoom-aware circle style ----

    // Maps Mapsui resolution to a symbol scale shared by all circle/ring styles.
    private static double CircleScaleForResolution(double resolution)
    {
        const double minScale = 0.12;
        const double maxScale = 0.50;
        const double logMin   = 9.21;
        const double logMax   = 14.73;
        var log = Math.Log(Math.Clamp(resolution, 10_000, 2_500_000));
        var t   = (log - logMin) / (logMax - logMin);
        return maxScale - t * (maxScale - minScale);
    }

    private sealed class ZoomCircleStyle : BaseStyle, IThemeStyle
    {
        private readonly MapsuiColor _fill;
        private readonly MapsuiColor _outline;
        private readonly float _outlineWidth;
        private readonly double _scaleMultiplier;
        private SymbolStyle? _cached;
        private double _cachedResolution = -1;

        // Pass null fill for a transparent (ring-only) style.
        public ZoomCircleStyle(MapsuiColor? fill, MapsuiColor outline, float outlineWidth, double scaleMultiplier = 1.0)
        {
            _fill            = fill ?? new MapsuiColor(0, 0, 0, 0);
            _outline         = outline;
            _outlineWidth    = outlineWidth;
            _scaleMultiplier = scaleMultiplier;
        }

        public IStyle? GetStyle(IFeature feature, Viewport viewport)
        {
            var res = viewport.Resolution;
            if (_cached != null &&
                Math.Abs(res - _cachedResolution) / Math.Max(1.0, _cachedResolution) < 0.05)
                return _cached;

            _cachedResolution = res;
            _cached = new SymbolStyle
            {
                Fill        = new MapsuiBrush(_fill),
                Outline     = new MapsuiPen(_outline, _outlineWidth),
                Line        = null,
                SymbolScale = CircleScaleForResolution(res) * _scaleMultiplier,
            };
            return _cached;
        }
    }

    // Ring style for landed airports: inner position when landed-only, outer when also departed.
    // Features with feature["ring_outer"] == true use the outer (larger) scale.
    private sealed class RingStyle : BaseStyle, IThemeStyle
    {
        private readonly MapsuiColor _color;
        private readonly float _outlineWidth;
        private readonly double _innerMultiplier;
        private readonly double _outerMultiplier;
        private SymbolStyle? _cachedInner;
        private SymbolStyle? _cachedOuter;
        private double _cachedResolution = -1;

        public RingStyle(MapsuiColor color, float outlineWidth, double innerMultiplier, double outerMultiplier)
        {
            _color           = color;
            _outlineWidth    = outlineWidth;
            _innerMultiplier = innerMultiplier;
            _outerMultiplier = outerMultiplier;
        }

        public IStyle? GetStyle(IFeature feature, Viewport viewport)
        {
            var res = viewport.Resolution;
            if (_cachedInner is null ||
                Math.Abs(res - _cachedResolution) / Math.Max(1.0, _cachedResolution) >= 0.05)
            {
                _cachedResolution = res;
                var baseScale = CircleScaleForResolution(res);
                _cachedInner = MakeSymbol(baseScale * _innerMultiplier);
                _cachedOuter = MakeSymbol(baseScale * _outerMultiplier);
            }
            return (feature["ring_outer"] as bool? == true) ? _cachedOuter : _cachedInner;
        }

        private SymbolStyle MakeSymbol(double scale) => new SymbolStyle
        {
            Fill        = new MapsuiBrush(new MapsuiColor(0, 0, 0, 0)),
            Outline     = new MapsuiPen(_color, _outlineWidth),
            Line        = null,
            SymbolScale = scale,
        };
    }
}
