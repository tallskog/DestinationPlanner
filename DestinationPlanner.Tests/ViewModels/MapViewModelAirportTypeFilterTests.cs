using DestinationPlanner.Models;
using DestinationPlanner.Tests.Fakes;
using DestinationPlanner.ViewModels;

namespace DestinationPlanner.Tests.ViewModels;

public class MapViewModelAirportTypeFilterTests
{
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
        return new MapViewModel(airports, logbook, new FakeSimConnectService());
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
        vm.ShowMilitaryAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "EFTU");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_HeliportUnchecked_ExcludesOnlyHeliport()
    {
        var vm = CreateViewModel(out _);
        vm.ShowHeliportAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "EFHF");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_OtherUnchecked_ExcludesOnlyOther()
    {
        var vm = CreateViewModel(out _);
        vm.ShowOtherAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "LFLJ");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_UnknownUnchecked_ExcludesOnlyUnknown()
    {
        var vm = CreateViewModel(out _);
        vm.ShowUnknownAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "WWWW");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void GetAllFilteredAirports_UnclassifiedUnchecked_ExcludesOnlyUnclassified()
    {
        var vm = CreateViewModel(out _);
        vm.ShowUnclassifiedAirports = false;

        var result = vm.GetAllFilteredAirports();

        Assert.DoesNotContain(result, a => a.Icao == "ZZZZ");
        Assert.Equal(6, result.Count);
    }

    [Fact]
    public void ClearFiltersCommand_ResetsAirportTypeFiltersToAllVisible()
    {
        var vm = CreateViewModel(out _);
        vm.ShowMilitaryAirports = false;
        vm.ShowHeliportAirports = false;
        vm.ShowPrivateAirports = false;
        vm.ShowOtherAirports = false;
        vm.ShowUnknownAirports = false;
        vm.ShowUnclassifiedAirports = false;

        vm.ClearFiltersCommand.Execute(null);

        Assert.True(vm.ShowCivilAirports);
        Assert.True(vm.ShowMilitaryAirports);
        Assert.True(vm.ShowHeliportAirports);
        Assert.True(vm.ShowPrivateAirports);
        Assert.True(vm.ShowOtherAirports);
        Assert.True(vm.ShowUnknownAirports);
        Assert.True(vm.ShowUnclassifiedAirports);
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
        vm.ShowMilitaryAirports = false;

        var result = vm.GetLogbookAirports();

        Assert.DoesNotContain(result, a => a.Icao == "EFTU");
        Assert.Contains(result, a => a.Icao == "EFHK");
    }
}
