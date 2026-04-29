using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.ViewModels;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Tiling;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace DestinationPlanner.Views;

public partial class MapView : UserControl
{
    private MapViewModel? _vm;
    private MemoryLayer? _airportLayer;
    private MemoryLayer? _logbookLayer;
    private int _airportCount;
    private int _logbookCount;

    // Layer-level IThemeStyle implementations — GetStyle receives the current viewport
    // at render time, so the circle size scales automatically with zoom level.
    private static readonly ZoomCircleStyle AirportStyle = new(
        fill:         new Color(30, 120, 200, 200),
        outline:      new Color(0, 60, 130),
        outlineWidth: 1f);

    private static readonly ZoomCircleStyle LogbookStyle = new(
        fill:         new Color(220, 100, 0, 230),
        outline:      new Color(140, 50, 0),
        outlineWidth: 1.5f);

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

        // Centre on Europe once the viewport is ready
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

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (_airportLayer is null || _logbookLayer is null) return;

        var feature = e.GetMapInfo(new[] { _logbookLayer, _airportLayer })?.Feature;
        if (feature is null)
        {
            AirportPopup.IsOpen = false;
            return;
        }

        PopupIcao.Text   = feature["icao"] as string ?? string.Empty;
        PopupName.Text   = feature["name"] as string ?? string.Empty;
        PopupRunway.Text = feature["runway_ft"] is int ft && ft > 0
            ? $"Longest runway: {ft:N0} ft"
            : "Longest runway: N/A";
        PopupIls.Text    = feature["ils"] is true
            ? "Instrument approach: Yes"
            : "Instrument approach: No";

        AirportPopup.IsOpen = true;
    }

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

    // Zoom-aware layer style: returns a SymbolStyle whose scale smoothly shrinks as
    // the user zooms out, so markers don't clutter the continent view.
    private sealed class ZoomCircleStyle : BaseStyle, IThemeStyle
    {
        private readonly Color _fill;
        private readonly Color _outline;
        private readonly float _outlineWidth;

        // Cache the last computed style to avoid allocating on every render call.
        private SymbolStyle? _cached;
        private double _cachedResolution = -1;

        public ZoomCircleStyle(Color fill, Color outline, float outlineWidth)
        {
            _fill = fill;
            _outline = outline;
            _outlineWidth = outlineWidth;
        }

        public IStyle? GetStyle(IFeature feature, Viewport viewport)
        {
            var res = viewport.Resolution;

            // Rebuild only when resolution changes by more than 5 %.
            if (_cached != null &&
                Math.Abs(res - _cachedResolution) / Math.Max(1.0, _cachedResolution) < 0.05)
            {
                return _cached;
            }

            _cachedResolution = res;
            _cached = new SymbolStyle
            {
                Fill        = new Brush(_fill),
                Outline     = new Pen(_outline, _outlineWidth),
                Line        = null,
                SymbolScale = ScaleForResolution(res),
            };
            return _cached;
        }

        // Logarithmic mapping: SymbolScale 0.5 at city-level zoom (10 000 m/px)
        // down to 0.12 at continent-level zoom (2 500 000 m/px).
        private static double ScaleForResolution(double resolution)
        {
            const double minScale = 0.12;
            const double maxScale = 0.50;
            const double logMin   = 9.21;   // ln(10_000)
            const double logMax   = 14.73;  // ln(2_500_000)

            var log = Math.Log(Math.Clamp(resolution, 10_000, 2_500_000));
            var t   = (log - logMin) / (logMax - logMin);   // 0 = zoomed in, 1 = zoomed out
            return maxScale - t * (maxScale - minScale);
        }
    }
}
