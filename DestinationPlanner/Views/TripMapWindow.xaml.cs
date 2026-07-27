using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using MapsuiBrush = Mapsui.Styles.Brush;
using MapsuiColor = Mapsui.Styles.Color;
using MapsuiPen = Mapsui.Styles.Pen;
using Mapsui.Tiling;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DestinationPlanner.Views;

// Read-only overview of a saved trip plan (US41 follow-up): airport markers plus a straight
// line per leg, colored by Flown/Planned status. Deliberately a separate, self-contained
// map (own MapControl/Canvas) rather than reusing MapView/MapViewModel — this window shows
// a fixed route snapshot, none of the Map tab's filtering/live-aircraft/weather concerns apply.
//
// Clicking an airport marker or a leg line shows the same info popup style as the Map tab
// (ICAO, name, runways, METAR) — a plain marker click shows just that airport (Primary popup);
// clicking a leg's line shows both its airports at once (Primary + Secondary), since a single
// line inherently involves two airports.
public partial class TripMapWindow : Window
{
    private readonly TripPlan _plan;
    private readonly IAirportDataService _airportData;
    private readonly IMetarService _metarService = new MetarService();
    private readonly List<(TripLeg Leg, Airport From, Airport To, Line Visible, Line HitTarget, Line Highlight)> _routeLines = [];

    private MemoryLayer? _airportLayer;
    private Airport? _primaryAirport;
    private Airport? _secondaryAirport;
    private CancellationTokenSource? _primaryMetarCts;
    private CancellationTokenSource? _secondaryMetarCts;

    private static readonly SolidColorBrush RunwayForeground = new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

    // Halo drawn behind a leg's visible line when it's selected in the Legs grid — semi-
    // transparent so the underlying Flown/Planned color of the visible line stays legible.
    private static readonly SolidColorBrush HighlightBrush = new(System.Windows.Media.Color.FromArgb(170, 30, 144, 255));

    public TripMapWindow(TripPlan plan, IAirportDataService airportData)
    {
        InitializeComponent();
        _plan = plan;
        _airportData = airportData;
        TitleText.Text = plan.Title;
        Loaded += OnLoaded;
        LocationChanged += OnWindowLocationChanged;
    }

    // AllowsTransparency popups don't reposition automatically when their host window moves
    // (this window is its own top-level Window, unlike MapView's embedded UserControl, but the
    // same WPF quirk applies — see MapView.OnMainWindowLocationChanged/NudgePopup, US17.7/US17.8).
    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        NudgePopup(PrimaryPopup);
        NudgePopup(SecondaryPopup);
    }

    private static void NudgePopup(Popup popup)
    {
        if (!popup.IsOpen) return;
        var h = popup.HorizontalOffset;
        popup.HorizontalOffset = h + 1;
        popup.HorizontalOffset = h;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Best-effort: a leg whose airport isn't in the currently loaded dataset is simply
        // skipped rather than failing the whole window.
        var airportsByIcao = new Dictionary<string, Airport>(StringComparer.OrdinalIgnoreCase);
        foreach (var leg in _plan.Legs)
        {
            if (_airportData.GetByIcao(leg.DepartureIcao) is { } from) airportsByIcao[leg.DepartureIcao] = from;
            if (_airportData.GetByIcao(leg.ArrivalIcao) is { } to) airportsByIcao[leg.ArrivalIcao] = to;
        }

        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        _airportLayer = new MemoryLayer
        {
            Name = "TripAirports",
            Style = new SymbolStyle
            {
                Fill = new MapsuiBrush(new MapsuiColor(30, 100, 220, 220)),
                Outline = new MapsuiPen(new MapsuiColor(10, 50, 140), 1.5f),
                SymbolScale = 0.6,
            },
            Features = airportsByIcao.Values.Select(MakeFeature).ToList(),
        };
        map.Layers.Add(_airportLayer);

        MapCtrl.Map = map;
        MapCtrl.Info += OnMapInfo;

        foreach (var leg in _plan.Legs)
        {
            if (!airportsByIcao.TryGetValue(leg.DepartureIcao, out var from)) continue;
            if (!airportsByIcao.TryGetValue(leg.ArrivalIcao, out var to)) continue;

            var visible = new Line
            {
                Stroke = new SolidColorBrush(leg.Status == TripLegStatus.Flown
                    ? System.Windows.Media.Color.FromArgb(220, 40, 164, 40)   // green — flown
                    : System.Windows.Media.Color.FromArgb(220, 220, 100, 0)), // orange — planned
                StrokeThickness = 3,
                IsHitTestVisible = false,
            };

            // A 3px-thick visible line is a precise click target — pair it with a wider,
            // invisible line that actually receives the click (Transparent, unlike a null
            // brush, still participates in WPF hit-testing).
            var hitTarget = new Line
            {
                Stroke = Brushes.Transparent,
                StrokeThickness = 16,
                Cursor = Cursors.Hand,
            };
            var highlight = new Line
            {
                Stroke = HighlightBrush,
                StrokeThickness = 9,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            var legFrom = from;
            var legTo = to;
            hitTarget.MouseLeftButtonDown += (_, args) =>
            {
                // The hit-target line's endpoints sit exactly on the two airport markers, so a
                // click intended for an airport (anywhere within its marker) would otherwise be
                // captured by this line first — RouteOverlay is drawn above MapCtrl, so Mapsui's
                // own marker-click detection (OnMapInfo) never even sees it. Resolve it here
                // instead: a click near either endpoint means "that airport", not "this leg".
                var clickPoint = args.GetPosition(RouteOverlay);
                var (fx, fy) = MercatorToScreen(GeoHelper.LonToMercatorX(legFrom.Longitude), GeoHelper.LatToMercatorY(legFrom.Latitude));
                var (tx, ty) = MercatorToScreen(GeoHelper.LonToMercatorX(legTo.Longitude), GeoHelper.LatToMercatorY(legTo.Latitude));

                if (Distance(clickPoint.X, clickPoint.Y, fx, fy) <= AirportClickRadiusPx)
                    ShowSingleAirport(legFrom);
                else if (Distance(clickPoint.X, clickPoint.Y, tx, ty) <= AirportClickRadiusPx)
                    ShowSingleAirport(legTo);
                else
                    ShowLegAirports(legFrom, legTo);

                args.Handled = true;
            };

            // Highlight drawn first so it sits behind the visible line and its wider hit-target.
            RouteOverlay.Children.Add(highlight);
            RouteOverlay.Children.Add(visible);
            RouteOverlay.Children.Add(hitTarget);
            _routeLines.Add((leg, from, to, visible, hitTarget, highlight));
        }

        map.Navigator.ViewportChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            RepositionRouteLines();
            RepositionOpenPopups();
        });

        if (airportsByIcao.Count > 0)
        {
            double minLat = airportsByIcao.Values.Min(a => a.Latitude);
            double maxLat = airportsByIcao.Values.Max(a => a.Latitude);
            double minLon = airportsByIcao.Values.Min(a => a.Longitude);
            double maxLon = airportsByIcao.Values.Max(a => a.Longitude);

            double cx = GeoHelper.LonToMercatorX((minLon + maxLon) / 2.0);
            double cy = GeoHelper.LatToMercatorY((minLat + maxLat) / 2.0);

            // Resolution picked so the whole bounding box fits the window with some padding;
            // floored so a single airport (or two very close ones) doesn't over-zoom.
            double xSpan = Math.Abs(GeoHelper.LonToMercatorX(maxLon) - GeoHelper.LonToMercatorX(minLon));
            double ySpan = Math.Abs(GeoHelper.LatToMercatorY(maxLat) - GeoHelper.LatToMercatorY(minLat));
            double resolution = Math.Max(500, Math.Max(xSpan / 700.0, ySpan / 450.0) * 1.4);

            map.ViewportInitialized += (_, _) =>
                map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), resolution);
        }
    }

    // ---- Airport marker clicks (Mapsui MapControl.Info) ----

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (_airportLayer is null) return;

        var feature = e.GetMapInfo([_airportLayer])?.Feature;
        if (feature is null)
        {
            // Clicking empty map area (not on a marker, and not on/near a leg line — see the
            // hit-target handler below) is the only way to dismiss an open popup, since a
            // WPF Popup doesn't auto-close on an outside click the way a native context menu
            // does. Mirrors MapView.OnMapInfo's null-feature branch.
            ClosePopup(PrimaryPopup, ref _primaryMetarCts);
            ClosePopup(SecondaryPopup, ref _secondaryMetarCts);
            _primaryAirport = null;
            _secondaryAirport = null;
            return;
        }

        var icao = feature["icao"] as string ?? string.Empty;
        var airport = _airportData.GetByIcao(icao);
        if (airport is null) return;

        ShowSingleAirport(airport);
    }

    // A plain marker click (or a line click landing within AirportClickRadiusPx of an endpoint)
    // always shows just that one airport — matches the Map tab's plain-left-click behavior.
    private void ShowSingleAirport(Airport airport)
    {
        _primaryAirport = airport;
        _secondaryAirport = null;
        ClosePopup(SecondaryPopup, ref _secondaryMetarCts);
        OpenPopup(airport, isPrimary: true);
    }

    // ---- Leg line clicks ----

    private const double AirportClickRadiusPx = 14;

    private void ShowLegAirports(Airport from, Airport to)
    {
        _primaryAirport = from;
        _secondaryAirport = to;
        OpenPopup(from, isPrimary: true);
        OpenPopup(to, isPrimary: false);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // ---- Popup management (mirrors MapView's Primary/Secondary popup pattern) ----

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

        var icaoBlock = isPrimary ? PrimaryIcao : SecondaryIcao;
        var nameBlock = isPrimary ? PrimaryName : SecondaryName;
        var runwaysList = isPrimary ? PrimaryRunwaysList : SecondaryRunwaysList;
        var metarBlock = isPrimary ? PrimaryMetar : SecondaryMetar;

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

    private static void ClosePopup(Popup popup, ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        popup.IsOpen = false;
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

    // A Popup is a separate top-level window in WPF and isn't clipped by its logical parent —
    // close it once its anchor pans/zooms outside the map area rather than let it float over
    // nothing, matching MapView.SetPopupPosition.
    private void SetPopupPosition(Popup popup, Airport airport)
    {
        var (sx, sy) = MercatorToScreen(
            GeoHelper.LonToMercatorX(airport.Longitude),
            GeoHelper.LatToMercatorY(airport.Latitude));

        if (sx < 0 || sy < 0 || sx > MapCtrl.ActualWidth || sy > MapCtrl.ActualHeight)
        {
            popup.IsOpen = false;
            return;
        }

        popup.HorizontalOffset = sx + 10;
        popup.VerticalOffset = sy + 10;
    }

    private void RepositionOpenPopups()
    {
        if (_primaryAirport is not null && PrimaryPopup.IsOpen) SetPopupPosition(PrimaryPopup, _primaryAirport);
        if (_secondaryAirport is not null && SecondaryPopup.IsOpen) SetPopupPosition(SecondaryPopup, _secondaryAirport);
    }

    private void RepositionRouteLines()
    {
        foreach (var (_, from, to, visible, hitTarget, highlight) in _routeLines)
        {
            var (x1, y1) = MercatorToScreen(GeoHelper.LonToMercatorX(from.Longitude), GeoHelper.LatToMercatorY(from.Latitude));
            var (x2, y2) = MercatorToScreen(GeoHelper.LonToMercatorX(to.Longitude), GeoHelper.LatToMercatorY(to.Latitude));
            visible.X1 = hitTarget.X1 = highlight.X1 = x1;
            visible.Y1 = hitTarget.Y1 = highlight.Y1 = y1;
            visible.X2 = hitTarget.X2 = highlight.X2 = x2;
            visible.Y2 = hitTarget.Y2 = highlight.Y2 = y2;
        }
    }

    // Called from TripPlanView when the Legs grid's selection changes, so the map stays in
    // sync with whichever leg(s) are currently selected in the list (multi-select supported —
    // every leg whose Order is in the set gets its halo shown, everything else's is hidden).
    public void HighlightLegs(IReadOnlySet<int> legOrders)
    {
        foreach (var (leg, _, _, _, _, highlight) in _routeLines)
            highlight.Visibility = legOrders.Contains(leg.Order) ? Visibility.Visible : Visibility.Collapsed;
    }

    // Converts Mercator world coords to MapCtrl-relative screen pixel coords (no rotation) —
    // same formula as MapView.MercatorToScreen.
    private (double x, double y) MercatorToScreen(double mercX, double mercY)
    {
        var vp = MapCtrl.Map.Navigator.Viewport;
        var sx = (mercX - vp.CenterX) / vp.Resolution + vp.Width / 2.0;
        var sy = (mercY - vp.CenterY) / vp.Resolution * -1.0 + vp.Height / 2.0;
        return (sx, sy);
    }

    private static PointFeature MakeFeature(Airport airport)
    {
        var x = GeoHelper.LonToMercatorX(airport.Longitude);
        var y = GeoHelper.LatToMercatorY(airport.Latitude);
        var feature = new PointFeature(new MPoint(x, y));
        feature["icao"] = airport.Icao;
        feature["name"] = airport.Name;
        return feature;
    }
}
