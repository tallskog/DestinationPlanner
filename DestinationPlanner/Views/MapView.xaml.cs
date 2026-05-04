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

namespace DestinationPlanner.Views;

public partial class MapView : UserControl
{
    private MapViewModel? _vm;
    private MemoryLayer? _airportLayer;
    private MemoryLayer? _logbookLayer;
    private int _airportCount;
    private int _logbookCount;

    private Airport? _primaryAirport;
    private Airport? _secondaryAirport;

    private CancellationTokenSource? _primaryMetarCts;
    private CancellationTokenSource? _secondaryMetarCts;

    private readonly IMetarService _metarService = new MetarService();

    // WPF elements drawn on the Canvas overlay for the selection line
    private Line? _selectionLine;
    private TextBlock? _selectionDistLabel;

    private static readonly ZoomCircleStyle AirportStyle = new(
        fill:         new MapsuiColor(30, 120, 200, 200),
        outline:      new MapsuiColor(0, 60, 130),
        outlineWidth: 1f);

    private static readonly ZoomCircleStyle LogbookStyle = new(
        fill:         new MapsuiColor(220, 100, 0, 230),
        outline:      new MapsuiColor(140, 50, 0),
        outlineWidth: 1.5f);

    // Pre-built WPF brushes used in MakeRunwayTextBlock (avoids per-call allocation)
    private static readonly SolidColorBrush RunwayForeground =
        new(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44));

    public MapView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = DataContext as MapViewModel;
        if (_vm is null) return;

        _vm.FiltersApplied += (_, _) => Dispatcher.Invoke(() => { RefreshAirportLayer(); RefreshLogbookLayer(); });
        _vm.LogbookChanged += (_, _) => Dispatcher.Invoke(RefreshLogbookLayer);
        _vm.PropertyChanged += OnVmPropertyChanged;
        MapCtrl.Info += OnMapInfo;

        _airportLayer = new MemoryLayer { Name = "Airports", Style = AirportStyle };
        _logbookLayer = new MemoryLayer { Name = "Logbook",  Style = LogbookStyle };

        var map = new Mapsui.Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer());
        map.Layers.Add(_airportLayer);
        map.Layers.Add(_logbookLayer);

        MapCtrl.Map = map;

        map.Navigator.ViewportChanged += (_, _) => Dispatcher.Invoke(OnViewportChanged);

        var cx = GeoHelper.LonToMercatorX(15.0);
        var cy = GeoHelper.LatToMercatorY(50.0);
        map.ViewportInitialized += (_, _) =>
            map.Navigator.CenterOnAndZoomTo(new MPoint(cx, cy), 2_500_000);
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.FilterCenterIcao) or nameof(MapViewModel.FilterRadiusNm))
            Dispatcher.Invoke(RefreshAirportLayer);
    }

    // ---- Layer refresh ----

    private void RefreshLogbookLayer()
    {
        if (_vm is null || _logbookLayer is null) return;
        var airports = _vm.GetLogbookAirports();
        _logbookLayer.Features = airports.Select(MakeFeature).ToList();
        _logbookLayer.DataHasChanged();
        _logbookCount = airports.Count;
        UpdateStatus();
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
        if (_airportLayer is null || _logbookLayer is null) return;

        var feature = e.GetMapInfo(new[] { _logbookLayer, _airportLayer })?.Feature;

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
        SelectionOverlay.Children.Clear();
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

    private sealed class ZoomCircleStyle : BaseStyle, IThemeStyle
    {
        private readonly MapsuiColor _fill;
        private readonly MapsuiColor _outline;
        private readonly float _outlineWidth;
        private SymbolStyle? _cached;
        private double _cachedResolution = -1;

        public ZoomCircleStyle(MapsuiColor fill, MapsuiColor outline, float outlineWidth)
        {
            _fill         = fill;
            _outline      = outline;
            _outlineWidth = outlineWidth;
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
                SymbolScale = ScaleForResolution(res),
            };
            return _cached;
        }

        private static double ScaleForResolution(double resolution)
        {
            const double minScale = 0.12;
            const double maxScale = 0.50;
            const double logMin   = 9.21;
            const double logMax   = 14.73;
            var log = Math.Log(Math.Clamp(resolution, 10_000, 2_500_000));
            var t   = (log - logMin) / (logMax - logMin);
            return maxScale - t * (maxScale - minScale);
        }
    }
}
