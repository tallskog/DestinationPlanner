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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DestinationPlanner.Views;

// Read-only single-leg view of one logged flight (US46), opened from the Logbook tab's
// "Show on Map" context menu item. A fourth map surface alongside MapView/TripMapWindow/
// CandidateMapWindow — see CLAUDE.md's "Map info box parity" rule and Helpers/DraggableInfoBox.
// Deliberately simpler than TripMapWindow: exactly one fixed leg, so there's no Flown/Planned
// coloring and no halo/HighlightLegs sync (that existed only for TripPlanView's multi-row Legs
// grid selection). The one deliberate departure from the other three windows: since the leg
// is fixed and already known, both info boxes open automatically on load instead of requiring
// a click — every actual interaction (click a marker for a single box, click the line for both,
// click empty space to close, drag, reposition-on-pan/zoom) is the same shared DraggableInfoBox
// behavior as the other windows, just invoked once up front.
public partial class FlightLegMapWindow : Window
{
    private readonly FlightRecord _flight;
    private readonly IAirportDataService _airportData;
    private readonly IMetarService _metarService = new MetarService();

    private MemoryLayer? _airportLayer;
    private Airport? _from;
    private Airport? _to;
    private Airport? _primaryAirport;
    private Airport? _secondaryAirport;
    private CancellationTokenSource? _primaryMetarCts;
    private CancellationTokenSource? _secondaryMetarCts;
    private DraggableInfoBox _primaryBox = null!;
    private DraggableInfoBox _secondaryBox = null!;

    private Line? _visibleLine;
    private Line? _hitTargetLine;
    private TextBlock? _distanceLabel;

    private const double AirportClickRadiusPx = 14;

    private static readonly SolidColorBrush RunwayForeground = new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

    public FlightLegMapWindow(FlightRecord flight, IAirportDataService airportData)
    {
        InitializeComponent();
        _flight = flight;
        _airportData = airportData;
        TitleText.Text = $"{flight.DepartureIcao} → {flight.ArrivalIcao} — {flight.Date:yyyy-MM-dd}";
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _from = _airportData.GetByIcao(_flight.DepartureIcao);
        _to = _airportData.GetByIcao(_flight.ArrivalIcao);

        if (_from is null || _to is null)
        {
            EmptyStateText.Visibility = Visibility.Visible;
            MapCtrl.Visibility = Visibility.Collapsed;
            RouteOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var from = _from;
        var to = _to;

        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        _airportLayer = new MemoryLayer
        {
            Name = "LegAirports",
            Style = new SymbolStyle
            {
                Fill = new MapsuiBrush(new MapsuiColor(30, 100, 220, 220)),
                Outline = new MapsuiPen(new MapsuiColor(10, 50, 140), 1.5f),
                SymbolScale = 0.6,
            },
            Features = [MakeFeature(from), MakeFeature(to)],
        };
        map.Layers.Add(_airportLayer);

        MapCtrl.Map = map;
        MapCtrl.Info += OnMapInfo;

        _primaryBox = new DraggableInfoBox(PrimaryInfoBox, PrimaryLeaderLine, RouteOverlay,
            () => MapCtrl.ActualWidth, () => MapCtrl.ActualHeight,
            () => _primaryAirport is null ? null : AirportScreenPoint(_primaryAirport));
        _secondaryBox = new DraggableInfoBox(SecondaryInfoBox, SecondaryLeaderLine, RouteOverlay,
            () => MapCtrl.ActualWidth, () => MapCtrl.ActualHeight,
            () => _secondaryAirport is null ? null : AirportScreenPoint(_secondaryAirport));

        // Dashed amber line + distance label — same visual style as MapView's own primary/
        // secondary selection line (MapView.xaml.cs UpdateSelectionLine), so a leg shown here
        // reads consistently with the Map tab's own two-airport selection.
        _visibleLine = new Line
        {
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 180, 80, 0)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            IsHitTestVisible = false,
        };
        _hitTargetLine = new Line
        {
            Stroke = Brushes.Transparent,
            StrokeThickness = 16,
            Cursor = Cursors.Hand,
        };
        _distanceLabel = new TextBlock
        {
            Text = $"{GeoHelper.DistanceNm(from.Latitude, from.Longitude, to.Latitude, to.Longitude):N0} nm",
            FontSize = 11,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 40, 0)),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 240, 200)),
            Padding = new Thickness(3, 1, 3, 1),
            IsHitTestVisible = false,
        };

        _hitTargetLine.MouseLeftButtonDown += (_, args) =>
        {
            var clickPoint = args.GetPosition(RouteOverlay);
            var (fx, fy) = AirportScreenPoint(from);
            var (tx, ty) = AirportScreenPoint(to);

            if (Distance(clickPoint.X, clickPoint.Y, fx, fy) <= AirportClickRadiusPx)
                ShowSingleAirport(from);
            else if (Distance(clickPoint.X, clickPoint.Y, tx, ty) <= AirportClickRadiusPx)
                ShowSingleAirport(to);
            else
                ShowLegAirports(from, to);

            args.Handled = true;
        };

        RouteOverlay.Children.Add(_visibleLine);
        RouteOverlay.Children.Add(_hitTargetLine);
        RouteOverlay.Children.Add(_distanceLabel);

        map.Navigator.ViewportChanged += (_, _) => Dispatcher.Invoke(() =>
        {
            RepositionLine();
            _primaryBox.Reposition();
            _secondaryBox.Reposition();
        });

        double minLat = Math.Min(from.Latitude, to.Latitude);
        double maxLat = Math.Max(from.Latitude, to.Latitude);
        double minLon = Math.Min(from.Longitude, to.Longitude);
        double maxLon = Math.Max(from.Longitude, to.Longitude);

        double cx = GeoHelper.LonToMercatorX((minLon + maxLon) / 2.0);
        double cy = GeoHelper.LatToMercatorY((minLat + maxLat) / 2.0);

        double xSpan = Math.Abs(GeoHelper.LonToMercatorX(maxLon) - GeoHelper.LonToMercatorX(minLon));
        double ySpan = Math.Abs(GeoHelper.LatToMercatorY(maxLat) - GeoHelper.LatToMercatorY(minLat));
        double resolution = Math.Max(500, Math.Max(xSpan / 700.0, ySpan / 450.0) * 1.4);

        map.ViewportInitialized += (_, _) =>
        {
            map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), resolution);
            ShowLegAirports(from, to);
        };
    }

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (_airportLayer is null || _from is null || _to is null) return;

        var feature = e.GetMapInfo([_airportLayer])?.Feature;
        if (feature is null)
        {
            CloseBox(_primaryBox, ref _primaryMetarCts);
            CloseBox(_secondaryBox, ref _secondaryMetarCts);
            _primaryAirport = null;
            _secondaryAirport = null;
            return;
        }

        var icao = feature["icao"] as string ?? string.Empty;
        var airport = string.Equals(icao, _from.Icao, StringComparison.OrdinalIgnoreCase) ? _from
            : string.Equals(icao, _to.Icao, StringComparison.OrdinalIgnoreCase) ? _to
            : null;
        if (airport is null) return;

        ShowSingleAirport(airport);
    }

    private void ShowSingleAirport(Airport airport)
    {
        _primaryAirport = airport;
        _secondaryAirport = null;
        CloseBox(_secondaryBox, ref _secondaryMetarCts);
        OpenInfoBox(airport, isPrimary: true);
    }

    private void ShowLegAirports(Airport from, Airport to)
    {
        _primaryAirport = from;
        _secondaryAirport = to;
        OpenInfoBox(from, isPrimary: true);
        OpenInfoBox(to, isPrimary: false);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void OpenInfoBox(Airport airport, bool isPrimary)
    {
        var box = isPrimary ? _primaryBox : _secondaryBox;
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

        box.Reposition();

        var cts = isPrimary ? _primaryMetarCts! : _secondaryMetarCts!;
        _ = LoadMetarAsync(airport.Icao, metarBlock, cts.Token);
    }

    private static void CloseBox(DraggableInfoBox box, ref CancellationTokenSource? cts)
    {
        cts?.Cancel();
        box.ResetOffset();
        box.Hide();
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

    private (double X, double Y) AirportScreenPoint(Airport airport) => MercatorToScreen(
        GeoHelper.LonToMercatorX(airport.Longitude),
        GeoHelper.LatToMercatorY(airport.Latitude));

    private void RepositionLine()
    {
        if (_from is null || _to is null || _visibleLine is null || _hitTargetLine is null || _distanceLabel is null) return;

        var (x1, y1) = MercatorToScreen(GeoHelper.LonToMercatorX(_from.Longitude), GeoHelper.LatToMercatorY(_from.Latitude));
        var (x2, y2) = MercatorToScreen(GeoHelper.LonToMercatorX(_to.Longitude), GeoHelper.LatToMercatorY(_to.Latitude));

        _visibleLine.X1 = _hitTargetLine.X1 = x1;
        _visibleLine.Y1 = _hitTargetLine.Y1 = y1;
        _visibleLine.X2 = _hitTargetLine.X2 = x2;
        _visibleLine.Y2 = _hitTargetLine.Y2 = y2;

        Canvas.SetLeft(_distanceLabel, (x1 + x2) / 2 - 22);
        Canvas.SetTop(_distanceLabel, (y1 + y2) / 2 - 10);
    }

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
