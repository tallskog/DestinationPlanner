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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

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

    // Draggable airport info boxes — see Helpers/DraggableInfoBox and CLAUDE.md's "Map info
    // box parity" rule (shared with TripMapWindow's trip-plan map).
    private DraggableInfoBox _primaryBox = null!;
    private DraggableInfoBox _secondaryBox = null!;

    private CancellationTokenSource? _primaryMetarCts;
    private CancellationTokenSource? _secondaryMetarCts;

    private readonly IMetarService _metarService = new MetarService();

    // Precipitation radar overlay (US38) — a Mapsui tile layer toggled on/off, refreshed only
    // on explicit user request (no background polling; see requirements.md US38).
    private readonly IPrecipitationRadarService _precipitationRadarService = new PrecipitationRadarService();
    private Mapsui.Tiling.Layers.TileLayer? _precipitationLayer;
    private CancellationTokenSource? _precipitationCts;

    // Wind barb overlay (US39) — WPF-drawn glyphs on SelectionOverlay (like the aircraft
    // marker), not a Mapsui layer, since barbs are composite vector shapes rather than tiles.
    // Refreshed only on explicit user request or flight-level change — no background polling.
    private readonly IWindDataService _windDataService = new WindDataService();
    private CancellationTokenSource? _windCts;
    private readonly List<UIElement> _windBarbElements = [];
    private IReadOnlyList<WindSample> _lastWindSamples = [];
    // Fetch grid: what's actually queried from Open-Meteo, kept safely under the ~450-500
    // point ceiling where their server starts rejecting the batched GET URL as too long
    // (empirically tested: 450 points/~7.3KB URL succeeds, 520 points/~8.4KB fails with a
    // literal nginx 414). Visual grid: what's actually drawn, via bilinear interpolation
    // (see InterpolateVisualGrid) — this is what makes the overlay look like a dense flow
    // field rather than a sparse set of dots, without needing one fetched point per barb.
    private const int WindFetchGridColumns = 16;
    private const int WindFetchGridRows = 11;
    private const int WindVisualGridColumns = 44;
    private const int WindVisualGridRows = 28;
    // At (near) whole-world zoom, the same fetch grid would be spread across the entire
    // globe — spacing points thousands of km apart, which isn't a usefully readable wind
    // picture, and spends an expensive batched request that makes tripping Open-Meteo's
    // rate limit (BUG-09/BUG-11) more likely on the very next real, in-area fetch. Chosen
    // generously above any normal continental/ocean-crossing view (empirically up to
    // ~35 degrees of longitude for a full-Europe view) while still well below a world span.
    private const double WindMaxViewportSpanDeg = 80.0;
    // Debounces re-sampling the wind grid after pan/zoom: the grid is sampled from the
    // viewport at fetch time, so zooming in on a world-wide sample can leave the new view
    // with no barbs at all unless the grid is refreshed for the new area. Re-fetching on
    // every intermediate ViewportChanged event during a drag/zoom gesture would spam the
    // API, so this timer resets on each change and only fires once the view settles.
    private DispatcherTimer? _windViewportDebounceTimer;
    private DispatcherTimer? _windCooldownWaitTimer;
    private static readonly TimeSpan WindViewportDebounceInterval = TimeSpan.FromMilliseconds(600);
    // A single settle is not enough on its own — a user doing several distinct pan/zoom
    // adjustments within a few seconds (e.g. zoom, pause, zoom again) would still fire one
    // auto-fetch per settle, and Open-Meteo's free/anonymous tier rate-limits (HTTP 429)
    // surprisingly quickly under that pattern. This additionally enforces a minimum gap
    // between auto-triggered fetches specifically; explicit user actions (Refresh click,
    // toggling on, changing flight level) are never throttled by this — only fetches that
    // happen purely because the map moved are.
    private DateTime _lastWindAutoFetchUtc = DateTime.MinValue;
    private static readonly TimeSpan WindAutoFetchCooldown = TimeSpan.FromSeconds(4);

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

    // Pre-built WPF brushes used in MakeRunwayTextBlock (avoids per-call allocation)
    private static readonly SolidColorBrush RunwayForeground =
        new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

    public MapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;

        WindFlightLevelCombo.ItemsSource = WindFlightLevel.Standard;
        WindFlightLevelCombo.SelectedIndex = 0;
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

    // ---- Wind barb overlay ----

    private async void WindToggle_Checked(object sender, RoutedEventArgs e) => await LoadWindBarbsAsync();

    private void WindToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _windCts?.Cancel();
        _windViewportDebounceTimer?.Stop();
        _windCooldownWaitTimer?.Stop();
        ClearWindBarbs();
        WindRefreshButton.IsEnabled = false;
        WindStatusText.Text = string.Empty;
        WindAttributionText.Visibility = Visibility.Collapsed;
    }

    private async void WindRefresh_Click(object sender, RoutedEventArgs e) => await LoadWindBarbsAsync();

    // Changing the flight level is itself an explicit user action (not background polling),
    // so it re-fetches immediately — but only while the overlay is actually on; otherwise this
    // just remembers the selection for the next time the toggle is checked. Also a no-op
    // during construction, before WindToggleButton has a real IsChecked state.
    private async void WindFlightLevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || WindToggleButton.IsChecked != true) return;
        await LoadWindBarbsAsync();
    }

    private async Task LoadWindBarbsAsync()
    {
        _windCts?.Cancel();
        var cts = new CancellationTokenSource();
        _windCts = cts;

        // Reset regardless of what triggered this fetch, so a manual Refresh/toggle/level
        // change also postpones the next viewport-triggered auto-fetch by the full cooldown
        // rather than letting one fire again immediately after.
        _lastWindAutoFetchUtc = DateTime.UtcNow;

        WindToggleButton.IsChecked = true;
        WindRefreshButton.IsEnabled = false;
        WindStatusText.Text = "loading…";

        var level = (WindFlightLevel)WindFlightLevelCombo.SelectedItem;
        var bounds = GetViewportLonLatBounds();

        if (bounds.MaxLon - bounds.MinLon > WindMaxViewportSpanDeg ||
            bounds.MaxLat - bounds.MinLat > WindMaxViewportSpanDeg)
        {
            WindStatusText.Text = "zoom in to see wind barbs";
            WindRefreshButton.IsEnabled = true;
            return;
        }

        var fetchPoints = BuildGridPoints(bounds, WindFetchGridColumns, WindFetchGridRows);

        var result = await _windDataService.GetWindGridAsync(fetchPoints, level.PressureHPa, cts.Token);

        if (cts.IsCancellationRequested) return;

        if (result.Failure is not null)
        {
            WindStatusText.Text = result.Failure switch
            {
                WindFetchFailure.RateLimited => "rate limited, try again shortly",
                WindFetchFailure.ServiceUnavailable => "Open-Meteo unavailable",
                _ => "network error",
            };
            WindRefreshButton.IsEnabled = true;
            return;
        }

        var samples = result.Samples;
        if (samples.Count == 0)
        {
            WindStatusText.Text = "no wind data for this area";
            WindRefreshButton.IsEnabled = true;
            return;
        }

        // Open-Meteo's free API is queried at a modest, URL-length-safe resolution
        // (empirically: batches beyond ~450-500 points hit the server's own URI-length
        // limit), then interpolated up to a much denser visual grid for rendering — the
        // same "coarse model grid, dense rendered flow field" approach real wind-map
        // visualizations use, rather than trying to fetch one point per drawn barb.
        IReadOnlyList<WindSample> visualSamples =
            samples.Count == WindFetchGridColumns * WindFetchGridRows
                ? InterpolateVisualGrid(samples, bounds)
                : samples; // partial fetch result — fall back to drawing exactly what we got

        _lastWindSamples = visualSamples;
        DrawWindBarbs(visualSamples);

        WindStatusText.Text = $"{visualSamples.Count} pts";
        WindRefreshButton.IsEnabled = true;
        WindAttributionText.Visibility = Visibility.Visible;
    }

    private (double MinLat, double MaxLat, double MinLon, double MaxLon) GetViewportLonLatBounds()
    {
        var vp = MapCtrl.Map.Navigator.Viewport;
        double halfW = vp.Width  / 2.0 * vp.Resolution;
        double halfH = vp.Height / 2.0 * vp.Resolution;

        double minLon = GeoHelper.MercatorXToLon(vp.CenterX - halfW);
        double maxLon = GeoHelper.MercatorXToLon(vp.CenterX + halfW);
        double minLat = Math.Clamp(GeoHelper.MercatorYToLat(vp.CenterY - halfH), -85.0, 85.0);
        double maxLat = Math.Clamp(GeoHelper.MercatorYToLat(vp.CenterY + halfH), -85.0, 85.0);
        return (minLat, maxLat, minLon, maxLon);
    }

    // Builds an evenly-spaced cols x rows grid of (lat, lon) within the given bounds,
    // row-major (matches the ordering GetWindGridAsync's results come back in).
    private static List<(double Lat, double Lon)> BuildGridPoints(
        (double MinLat, double MaxLat, double MinLon, double MaxLon) bounds, int cols, int rows)
    {
        var points = new List<(double, double)>(cols * rows);
        for (int row = 0; row < rows; row++)
        {
            double lat = bounds.MinLat + (row + 0.5) / rows * (bounds.MaxLat - bounds.MinLat);
            for (int col = 0; col < cols; col++)
            {
                double lon = bounds.MinLon + (col + 0.5) / cols * (bounds.MaxLon - bounds.MinLon);
                points.Add((lat, lon));
            }
        }
        return points;
    }

    // Bilinearly interpolates the coarse fetched grid up to WindVisualGridColumns x
    // WindVisualGridRows points. Interpolating via u/v vector components (not raw
    // direction degrees) is essential — naively averaging e.g. 350° and 10° gives 180°
    // (exactly backwards) instead of the correct ~0°, since direction wraps at 360°.
    private static List<WindSample> InterpolateVisualGrid(
        IReadOnlyList<WindSample> fetched, (double MinLat, double MaxLat, double MinLon, double MaxLon) bounds)
    {
        var visualPoints = BuildGridPoints(bounds, WindVisualGridColumns, WindVisualGridRows);
        var result = new List<WindSample>(visualPoints.Count);

        for (int vr = 0; vr < WindVisualGridRows; vr++)
        {
            double fracRow = WindFetchGridRows <= 1 ? 0 : (double)vr / (WindVisualGridRows - 1) * (WindFetchGridRows - 1);
            int r0 = Math.Clamp((int)Math.Floor(fracRow), 0, WindFetchGridRows - 1);
            int r1 = Math.Min(r0 + 1, WindFetchGridRows - 1);
            double tr = fracRow - r0;

            for (int vc = 0; vc < WindVisualGridColumns; vc++)
            {
                double fracCol = WindFetchGridColumns <= 1 ? 0 : (double)vc / (WindVisualGridColumns - 1) * (WindFetchGridColumns - 1);
                int c0 = Math.Clamp((int)Math.Floor(fracCol), 0, WindFetchGridColumns - 1);
                int c1 = Math.Min(c0 + 1, WindFetchGridColumns - 1);
                double tc = fracCol - c0;

                var s00 = fetched[r0 * WindFetchGridColumns + c0];
                var s01 = fetched[r0 * WindFetchGridColumns + c1];
                var s10 = fetched[r1 * WindFetchGridColumns + c0];
                var s11 = fetched[r1 * WindFetchGridColumns + c1];

                var (u00, v00) = ToVector(s00);
                var (u01, v01) = ToVector(s01);
                var (u10, v10) = ToVector(s10);
                var (u11, v11) = ToVector(s11);

                double u0 = u00 + (u01 - u00) * tc, v0 = v00 + (v01 - v00) * tc;
                double u1 = u10 + (u11 - u10) * tc, v1 = v10 + (v11 - v10) * tc;
                double u = u0 + (u1 - u0) * tr, v = v0 + (v1 - v0) * tr;

                var (lat, lon) = visualPoints[vr * WindVisualGridColumns + vc];
                result.Add(FromVector(lat, lon, u, v));
            }
        }
        return result;
    }

    private static (double U, double V) ToVector(WindSample s)
    {
        double rad = s.DirectionDeg * Math.PI / 180.0;
        return (-s.SpeedKt * Math.Sin(rad), -s.SpeedKt * Math.Cos(rad));
    }

    private static WindSample FromVector(double lat, double lon, double u, double v)
    {
        double speed = Math.Sqrt(u * u + v * v);
        double dir = (Math.Atan2(-u, -v) * 180.0 / Math.PI + 360.0) % 360.0;
        return new WindSample(lat, lon, dir, speed);
    }

    private void ClearWindBarbs()
    {
        foreach (var el in _windBarbElements) SelectionOverlay.Children.Remove(el);
        _windBarbElements.Clear();
        _lastWindSamples = [];
    }

    private void DrawWindBarbs(IReadOnlyList<WindSample> samples)
    {
        ClearWindBarbs();
        _lastWindSamples = samples;

        foreach (var sample in samples)
        {
            var barb = BuildWindBarbVisual(sample.DirectionDeg, sample.SpeedKt);
            PositionWindBarb(barb, sample);
            SelectionOverlay.Children.Add(barb);
            _windBarbElements.Add(barb);
        }
    }

    private void RepositionWindBarbs()
    {
        for (int i = 0; i < _windBarbElements.Count && i < _lastWindSamples.Count; i++)
            PositionWindBarb(_windBarbElements[i], _lastWindSamples[i]);
    }

    private void PositionWindBarb(UIElement barb, WindSample sample)
    {
        var (sx, sy) = MercatorToScreen(
            GeoHelper.LonToMercatorX(sample.Longitude), GeoHelper.LatToMercatorY(sample.Latitude));
        Canvas.SetLeft(barb, sx - WindBarbCanvasSize / 2.0);
        Canvas.SetTop(barb, sy - WindBarbCanvasSize / 2.0);
    }

    // Smaller still than before, to suit the much denser visual grid (WindVisualGridColumns
    // x WindVisualGridRows above) without the barbs overlapping into an unreadable mess —
    // same proportions throughout, just scaled down further.
    private const double WindBarbCanvasSize = 22.0;
    private const double WindBarbShaftLength = 14.0;
    private const double WindBarbSpacing = 2.5;
    private const double WindBarbTickLength = 5.0;
    private const double WindBarbStrokeThickness = 1.0;
    private static readonly SolidColorBrush WindBarbBrush = new(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x22));

    // Builds a wind barb pointing "up" (toward the direction the wind is coming FROM, at
    // rotation 0 = north) and rotates the whole shape clockwise by directionDeg — the same
    // compass-bearing convention already used for the aircraft marker, just without that
    // glyph's -90° correction since this shape's rest orientation already points north.
    //
    // Standard symbol (matches the classic aviation wind-barb chart): calm = a small ring;
    // each full tick = 10 kt, each half tick = 5 kt, each filled pennant = 50 kt. Ticks/pennants
    // are stacked from the tip inward (largest units nearest the tip), each angled toward the
    // tip like a feather rather than perpendicular to the shaft.
    private static Canvas BuildWindBarbVisual(double directionDeg, double speedKt)
    {
        double c = WindBarbCanvasSize / 2.0;
        var canvas = new Canvas { Width = WindBarbCanvasSize, Height = WindBarbCanvasSize, IsHitTestVisible = false };

        int rounded = (int)(Math.Round(speedKt / 5.0) * 5);

        if (rounded <= 0)
        {
            AddRing(canvas, c, c, 4.0);
            AddRing(canvas, c, c, 2.0);
            return canvas;
        }

        double tipY = c - WindBarbShaftLength;
        canvas.Children.Add(new Line
        {
            X1 = c, Y1 = c, X2 = c, Y2 = tipY,
            Stroke = WindBarbBrush, StrokeThickness = WindBarbStrokeThickness,
        });

        int pennants  = rounded / 50;
        int remainder = rounded % 50;
        int fullBarbs = remainder / 10;
        int halfBarb  = (remainder % 10) >= 5 ? 1 : 0;

        // y walks from the tip toward the base as each feature is placed, so larger units
        // (pennants) land nearest the tip and the half-barb (if any) lands nearest the base —
        // matching the standard chart convention.
        double y = tipY;
        for (int i = 0; i < pennants; i++)
        {
            canvas.Children.Add(new Polygon
            {
                Points = [new(c, y), new(c + WindBarbTickLength, y + WindBarbSpacing / 2.0), new(c, y + WindBarbSpacing)],
                Fill = WindBarbBrush,
            });
            y += WindBarbSpacing;
        }
        for (int i = 0; i < fullBarbs; i++)
        {
            AddTick(canvas, c, y, WindBarbTickLength);
            y += WindBarbSpacing;
        }
        if (halfBarb == 1)
        {
            AddTick(canvas, c, y, WindBarbTickLength / 2.0);
        }

        canvas.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
        canvas.RenderTransform = new RotateTransform(directionDeg);
        return canvas;
    }

    // A tick angled toward the tip (up) and to the side, like a feather — rather than
    // perpendicular to the shaft — matching the classic wind-barb chart look.
    private static void AddTick(Canvas canvas, double shaftX, double y, double length)
    {
        canvas.Children.Add(new Line
        {
            X1 = shaftX, Y1 = y, X2 = shaftX + length, Y2 = y - length * 0.35,
            Stroke = WindBarbBrush, StrokeThickness = WindBarbStrokeThickness,
        });
    }

    private static void AddRing(Canvas canvas, double centerX, double centerY, double radius)
    {
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = radius * 2, Height = radius * 2, Stroke = WindBarbBrush, StrokeThickness = WindBarbStrokeThickness,
        };
        canvas.Children.Add(ring);
        Canvas.SetLeft(ring, centerX - radius);
        Canvas.SetTop(ring, centerY - radius);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            // The info boxes are regular Canvas-hosted elements now (not top-level Popups),
            // so their Visibility/position/ManualOffset survive a tab switch on their own —
            // just refresh in case the map area was resized while this tab was hidden.
            OnViewportChanged();
            return;
        }
        _initialized = true;

        _primaryBox = new DraggableInfoBox(PrimaryInfoBox, PrimaryLeaderLine, SelectionOverlay,
            () => MapCtrl.ActualWidth, () => MapCtrl.ActualHeight,
            () => _primaryAirport is null ? null : AirportScreenPoint(_primaryAirport));
        _secondaryBox = new DraggableInfoBox(SecondaryInfoBox, SecondaryLeaderLine, SelectionOverlay,
            () => MapCtrl.ActualWidth, () => MapCtrl.ActualHeight,
            () => _secondaryAirport is null ? null : AirportScreenPoint(_secondaryAirport));

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

        // Fixed-interval debounce timer — its Interval is never mutated. Earlier this fired
        // the cooldown-wait by changing this same timer's Interval to the remaining cooldown
        // and restarting it, but OnViewportChanged's own Stop()/Start() (below) inherits
        // whatever Interval happens to be set — so once a cooldown reschedule had lengthened
        // it, continued pan/zoom kept resetting that now-multi-second wait before it could
        // ever fire, silently starving the update entirely. The cooldown wait now lives in
        // its own separate timer instead, so this one always means exactly "settled 600ms".
        _windViewportDebounceTimer = new DispatcherTimer { Interval = WindViewportDebounceInterval };
        _windViewportDebounceTimer.Tick += async (_, _) =>
        {
            _windViewportDebounceTimer!.Stop();
            if (WindToggleButton.IsChecked != true) return;

            var sinceLastAutoFetch = DateTime.UtcNow - _lastWindAutoFetchUtc;
            if (sinceLastAutoFetch < WindAutoFetchCooldown)
            {
                _windCooldownWaitTimer!.Interval = WindAutoFetchCooldown - sinceLastAutoFetch;
                _windCooldownWaitTimer.Start();
                return;
            }

            await LoadWindBarbsAsync(); // sets _lastWindAutoFetchUtc itself
        };

        // One-shot: fires exactly once, when a cooldown-deferred fetch is finally due.
        // OnViewportChanged cancels this on any further map movement (see below), so a
        // fetch never fires for a viewport the user has already moved away from.
        _windCooldownWaitTimer = new DispatcherTimer();
        _windCooldownWaitTimer.Tick += async (_, _) =>
        {
            _windCooldownWaitTimer!.Stop();
            if (WindToggleButton.IsChecked == true) await LoadWindBarbsAsync();
        };

        var cx = GeoHelper.LonToMercatorX(15.0);
        var cy = GeoHelper.LatToMercatorY(50.0);
        map.ViewportInitialized += (_, _) =>
            map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), 2_500_000);

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
            ClosePrimaryBox();
            CloseSecondaryBox();
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
            OpenInfoBox(airport, isPrimary: false);
        }
        else
        {
            // Plain left-click (or ctrl with no primary): set primary, clear secondary
            _primaryAirport = airport;
            _secondaryAirport = null;
            CloseSecondaryBox();
            OpenInfoBox(airport, isPrimary: true);
        }

        UpdateSelectionLine();
    }

    // ---- Info box management ----

    private void OpenInfoBox(Airport airport, bool isPrimary)
    {
        var box = isPrimary ? _primaryBox : _secondaryBox;
        // Fresh selection starts back at the default anchor-relative spot, not wherever a
        // previously-selected airport's box happened to be dragged to.
        box.ResetOffset();

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

        box.Reposition();

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

    private void ClosePrimaryBox()
    {
        _primaryMetarCts?.Cancel();
        _primaryBox.ResetOffset();
        _primaryBox.Hide();
    }

    private void CloseSecondaryBox()
    {
        _secondaryMetarCts?.Cancel();
        _secondaryBox.ResetOffset();
        _secondaryBox.Hide();
    }

    // ---- Info box positioning ----

    private void OnViewportChanged()
    {
        // Always recompute position (not gated on visibility) so a box that Reposition() hid
        // while its airport panned off-screen can reappear once it pans back into view.
        _primaryBox.Reposition();
        _secondaryBox.Reposition();
        UpdateSelectionLinePositions();
        RepositionAircraftMarker();
        RepositionWindBarbs();

        // Keep existing barbs tracking the map smoothly during the gesture itself
        // (RepositionWindBarbs above); once the view settles, re-sample a grid for
        // wherever the user ended up rather than leaving stale, possibly off-view points.
        if (WindToggleButton.IsChecked == true)
        {
            _windViewportDebounceTimer?.Stop();
            _windViewportDebounceTimer?.Start();
            // Cancel any pending cooldown-deferred fetch from an earlier settle — it would
            // be for a viewport the user has since moved away from; the debounce timer above
            // will decide fresh, once this new position settles, whether to fetch or wait.
            _windCooldownWaitTimer?.Stop();
        }
    }

    private (double X, double Y) AirportScreenPoint(Airport airport) => MercatorToScreen(
        GeoHelper.LonToMercatorX(airport.Longitude),
        GeoHelper.LatToMercatorY(airport.Latitude));

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
            Stroke           = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 180, 80, 0)),
            StrokeThickness  = 2,
            StrokeDashArray  = new DoubleCollection { 6, 3 },
            IsHitTestVisible = false,
        };

        _selectionDistLabel = new TextBlock
        {
            Text             = $"{distNm:N0} nm",
            FontSize         = 11,
            Foreground       = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 40, 0)),
            Background       = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 240, 200)),
            Padding          = new Thickness(3, 1, 3, 1),
            IsHitTestVisible = false,
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
        CloseSecondaryBox();
        UpdateSelectionLine();

        var cx = GeoHelper.LonToMercatorX(airport.Longitude);
        var cy = GeoHelper.LatToMercatorY(airport.Latitude);
        MapCtrl.Map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), 1_700);

        OpenInfoBox(airport, isPrimary: true);
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
