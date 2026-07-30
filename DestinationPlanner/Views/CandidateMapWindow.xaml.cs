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
using System.Windows.Media;

namespace DestinationPlanner.Views;

// Read-only review map for the Trip Plans tab's current candidate list (US43) — a snapshot
// passed in at construction time (the window does not live-update if candidates are added or
// removed from the tab while it's open). Deliberately simpler than TripMapWindow (which shows
// a saved TripPlan's legs/route lines): candidates are a flat, unordered list with no "leg"
// concept, so there are no route lines, no Flown/Planned coloring, and only a single (primary)
// info box — see CLAUDE.md's "Map info box parity" rule and Helpers/DraggableInfoBox.
public partial class CandidateMapWindow : Window
{
    private readonly IReadOnlyList<Airport> _candidates;
    private readonly Dictionary<string, Airport> _byIcao;
    private readonly IMetarService _metarService = new MetarService();

    private MemoryLayer? _airportLayer;
    private Airport? _primaryAirport;
    private CancellationTokenSource? _primaryMetarCts;
    private DraggableInfoBox _primaryBox = null!;

    private static readonly SolidColorBrush RunwayForeground = new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

    public CandidateMapWindow(IReadOnlyList<Airport> candidates)
    {
        InitializeComponent();
        _candidates = candidates;
        _byIcao = candidates.ToDictionary(a => a.Icao, StringComparer.OrdinalIgnoreCase);
        TitleText.Text = $"{candidates.Count} candidate airport(s)";
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        _airportLayer = new MemoryLayer
        {
            Name = "CandidateAirports",
            Style = new SymbolStyle
            {
                Fill = new MapsuiBrush(new MapsuiColor(30, 100, 220, 220)),
                Outline = new MapsuiPen(new MapsuiColor(10, 50, 140), 1.5f),
                SymbolScale = 0.6,
            },
            Features = _candidates.Select(MakeFeature).ToList(),
        };
        map.Layers.Add(_airportLayer);

        MapCtrl.Map = map;
        MapCtrl.Info += OnMapInfo;

        _primaryBox = new DraggableInfoBox(PrimaryInfoBox, PrimaryLeaderLine, MarkerOverlay,
            () => MapCtrl.ActualWidth, () => MapCtrl.ActualHeight,
            () => _primaryAirport is null ? null : AirportScreenPoint(_primaryAirport));

        map.Navigator.ViewportChanged += (_, _) => Dispatcher.Invoke(() => _primaryBox.Reposition());

        if (_candidates.Count > 0)
        {
            double minLat = _candidates.Min(a => a.Latitude);
            double maxLat = _candidates.Max(a => a.Latitude);
            double minLon = _candidates.Min(a => a.Longitude);
            double maxLon = _candidates.Max(a => a.Longitude);

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

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (_airportLayer is null) return;

        var feature = e.GetMapInfo([_airportLayer])?.Feature;
        if (feature is null)
        {
            // Clicking empty map area closes any open box, mirroring MapView.OnMapInfo's
            // null-feature branch.
            CloseBox();
            _primaryAirport = null;
            return;
        }

        var icao = feature["icao"] as string ?? string.Empty;
        if (!_byIcao.TryGetValue(icao, out var airport)) return;

        _primaryAirport = airport;
        OpenInfoBox(airport);
    }

    private void OpenInfoBox(Airport airport)
    {
        _primaryBox.ResetOffset();

        _primaryMetarCts?.Cancel();
        _primaryMetarCts = new CancellationTokenSource();

        PrimaryIcao.Text = airport.Icao;
        PrimaryName.Text = airport.Name;

        PrimaryRunwaysList.Children.Clear();
        if (airport.Runways.Count > 0)
        {
            foreach (var rwy in airport.Runways)
                PrimaryRunwaysList.Children.Add(MakeRunwayLine($"  {rwy.Ident}: {rwy.LengthFt:N0} ft"));
        }
        else if (airport.LongestRunwayFt > 0)
        {
            PrimaryRunwaysList.Children.Add(MakeRunwayLine($"  {airport.LongestRunwayFt:N0} ft"));
        }
        else
        {
            PrimaryRunwaysList.Children.Add(MakeRunwayLine("  N/A"));
        }

        PrimaryMetar.Text = "METAR: Loading…";

        _primaryBox.Reposition();

        var cts = _primaryMetarCts;
        _ = LoadMetarAsync(airport.Icao, PrimaryMetar, cts.Token);
    }

    private void CloseBox()
    {
        _primaryMetarCts?.Cancel();
        _primaryBox.ResetOffset();
        _primaryBox.Hide();
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
