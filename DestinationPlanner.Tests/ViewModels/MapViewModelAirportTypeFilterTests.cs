using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Tests.Fakes;
using DestinationPlanner.ViewModels;
using System.IO;

namespace DestinationPlanner.Tests.ViewModels;

// IDisposable: MapViewModel writes filter settings via AppSettingsService.Save on Apply/Clear.
// Without redirecting it to a throwaway file, tests would overwrite the real dev settings.json
// (AppDataHelper resolves to the same DEBUG AppData folder used by a real dev build of the app).
public class MapViewModelAirportTypeFilterTests : IDisposable
{
    private readonly string _tempSettingsPath =
        Path.Combine(Path.GetTempPath(), $"dp-test-settings-{Guid.NewGuid():N}.json");

    public MapViewModelAirportTypeFilterTests()
    {
        AppSettingsService.TestOverridePath = _tempSettingsPath;
    }

    public void Dispose()
    {
        AppSettingsService.TestOverridePath = null;
        try { File.Delete(_tempSettingsPath); } catch { /* best-effort cleanup */ }
    }

    private static MapViewModel CreateViewModel(out FakeLogbookService logbook)
    {
        var airports = new FakeAirportDataService(new[]
        {
            new Airport { Icao = "EFHK", Name = "Civil Field",        Latitude = 60.0, Longitude = 24.0, Type = AirportType.Civil },
            new Airport { Icao = "EFTU", Name = "Military Field",     Latitude = 60.5, Longitude = 22.2, Type = AirportType.Military },
            new Airport { Icao = "EFHF", Name = "Heliport Field",     Latitude = 60.2, Longitude = 24.9, Type = AirportType.Heliport },
            new Airport { Icao = "EGLL", Name = "Private Field",      Latitude = 51.4, Longitude = -0.4, Type = AirportType.Private },
            new Airport { Icao = "LFLJ", Name = "Other Field",        Latitude = 45.4, Longitude = 6.6,  Type = AirportType.Other },
            new Airport { Icao = "WWWW", Name = "Unknown Field",      Latitude = 5.0,  Longitude = 100.0, Type = AirportType.Unknown },
            new Airport { Icao = "ZZZZ", Name = "Unclassified Field", Latitude = 10.0, Longitude = 20.0, Type = AirportType.Unclassified },
        });
        logbook = new FakeLogbookService();
        return new MapViewModel(airports, logbook, new FakeSimConnectService(), new AppSettings());
    }

    [Fact]
    public void GetAllFilteredAirports_DefaultState_ReturnsAllAirports()
    {
        var vm = CreateViewModel(out _);

        var result = vm.GetAllFilteredAirports();

        Assert.Equal(7, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_MilitaryUnchecked_ExcludesOnlyMilitary()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowMilitaryAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "EFTU");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_HeliportUnchecked_ExcludesOnlyHeliport()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowHeliportAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "EFHF");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_OtherUnchecked_ExcludesOnlyOther()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowOtherAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "LFLJ");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_UnknownUnchecked_ExcludesOnlyUnknown()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowUnknownAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "WWWW");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_UnclassifiedUnchecked_ExcludesOnlyUnclassified()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowUnclassifiedAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "ZZZZ");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void ClearFiltersCommand_ResetsAirportTypeFiltersToAllVisible()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowMilitaryAirports = false;
        vm.Filters.ShowHeliportAirports = false;
        vm.Filters.ShowPrivateAirports = false;
        vm.Filters.ShowOtherAirports = false;
        vm.Filters.ShowUnknownAirports = false;
        vm.Filters.ShowUnclassifiedAirports = false;

        vm.Filters.ClearFiltersCommand.Execute(null);

        Assert.True(vm.Filters.ShowCivilAirports);
        Assert.True(vm.Filters.ShowMilitaryAirports);
        Assert.True(vm.Filters.ShowHeliportAirports);
        Assert.True(vm.Filters.ShowPrivateAirports);
        Assert.True(vm.Filters.ShowOtherAirports);
        Assert.True(vm.Filters.ShowUnknownAirports);
        Assert.True(vm.Filters.ShowUnclassifiedAirports);
        Assert.Equal(7, vm.GetAllFilteredAirports().Count);
    }

    // Guards against a BUG-05-style regression: the airport-type filter must be wired
    // into both GetAllFilteredAirports() and the shared ApplySharedFilters() helper used
    // by the logbook/departed/landed map layers, not just one of the two.
    [Fact]
    public void GetLogbookAirports_MilitaryUnchecked_ExcludesMilitaryVisitedAirport()
    {
        var vm = CreateViewModel(out var logbook);
        logbook.SetFlights([
            new FlightRecord { DepartureIcao = "EFTU", ArrivalIcao = "EFHK" },
        ]);
        vm.Filters.ShowMilitaryAirports = false;

        var result = vm.GetLogbookAirports();

        Assert.DoesNotContain(result, a => a.Icao == "EFTU");
        Assert.Contains(result, a => a.Icao == "EFHK");
    }
}
