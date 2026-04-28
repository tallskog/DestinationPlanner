using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.ViewModels;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Styles;
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

    // Styles — kept static so the same instances are shared across all features.
    private static readonly VectorStyle AirportStyle = new()
    {
        Fill    = new Brush(new Color(30, 120, 200, 200)),
        Outline = new Pen(new Color(0, 60, 130), 1),
        Line    = null,
    };

    private static readonly VectorStyle LogbookStyle = new()
    {
        Fill    = new Brush(new Color(220, 100, 0, 230)),
        Outline = new Pen(new Color(140, 50, 0), 1.5f),
        Line    = null,
    };

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

        _airportLayer = new MemoryLayer { Name = "Airports", Style = null };
        _logbookLayer = new MemoryLayer { Name = "Logbook",  Style = null };

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
        _logbookLayer.Features = airports.Select(a => MakeFeature(a, LogbookStyle)).ToList();
        _logbookLayer.DataHasChanged();

        _logbookCount = airports.Count;
        UpdateStatus();
    }

    private void RefreshAirportLayer()
    {
        if (_vm is null || _airportLayer is null) return;

        var airports = _vm.GetAllFilteredAirports();
        _airportLayer.Features = airports.Select(a => MakeFeature(a, AirportStyle)).ToList();
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

    private static PointFeature MakeFeature(Airport airport, VectorStyle style)
    {
        var x = GeoHelper.LonToMercatorX(airport.Longitude);
        var y = GeoHelper.LatToMercatorY(airport.Latitude);
        var feature = new PointFeature(new MPoint(x, y));
        feature["icao"]       = airport.Icao;
        feature["name"]       = airport.Name;
        feature["runway_ft"]  = airport.LongestRunwayFt;
        feature["ils"]        = airport.HasInstrumentApproach;
        feature.Styles.Add(style);
        return feature;
    }
}
