using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;

namespace DestinationPlanner.Tests.Services;

public class LogbookStatisticsServiceTests
{
    private static FakeAirportDataService CreateAirports() => new(new[]
    {
        new Airport { Icao = "EFHK", Name = "Helsinki",   Latitude = 60.3, Longitude = 24.9 },
        new Airport { Icao = "ENGM", Name = "Oslo",       Latitude = 60.2, Longitude = 11.1 },
        new Airport { Icao = "EKCH", Name = "Copenhagen", Latitude = 55.6, Longitude = 12.6 },
        new Airport { Icao = "LFPG", Name = "Paris CDG",  Latitude = 49.0, Longitude = 2.5 },
    });

    private static FlightRecord Flight(string dep, string arr, DateOnly date, DateTime blockOff, DateTime blockOn, string model = "C172") =>
        new()
        {
            DepartureIcao = dep,
            ArrivalIcao = arr,
            Date = date,
            AircraftModel = model,
            BlockOffUtc = blockOff,
            BlockOnUtc = blockOn,
        };

    [Fact]
    public void Calculate_EmptyLogbook_ReturnsZeroedResultWithoutThrowing()
    {
        var stats = LogbookStatisticsService.Calculate([], CreateAirports());

        Assert.Equal(0, stats.TotalFlights);
        Assert.Equal(0, stats.VisitedAirportCount);
        Assert.Empty(stats.TopVisitedAirports);
        Assert.Empty(stats.TopRoutes);
        Assert.Empty(stats.AircraftTypes);
        Assert.Null(stats.LongestLegByTime);
        Assert.Null(stats.LongestLegByDistance);
        Assert.Null(stats.AverageLegTime);
        Assert.Null(stats.AverageLegDistanceNm);
        Assert.Equal(TimeSpan.Zero, stats.TotalTime);
        Assert.Null(stats.TotalDistanceNm);
        Assert.Null(stats.FirstFlightDate);
        Assert.Null(stats.LastFlightDate);
    }

    [Fact]
    public void Calculate_VisitedAirportCount_IsDistinctAcrossDepartureAndArrival()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0)),
            Flight("ENGM", "EFHK", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 9, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        Assert.Equal(2, stats.VisitedAirportCount);
    }

    [Fact]
    public void Calculate_TopVisitedLandedDeparted_OrderedByCountDescending()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0)),
            Flight("EFHK", "EKCH", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 9, 0, 0)),
            Flight("EFHK", "LFPG", new DateOnly(2026, 1, 3), new DateTime(2026, 1, 3, 8, 0, 0), new DateTime(2026, 1, 3, 9, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        Assert.Equal("EFHK", stats.TopVisitedAirports[0].Icao);
        Assert.Equal(3, stats.TopVisitedAirports[0].Count);
        Assert.Equal("EFHK", stats.TopDepartedAirports[0].Icao);
        Assert.Equal(3, stats.TopDepartedAirports[0].Count);
        // EFHK never landed at in this dataset, so it should not lead the landed ranking.
        Assert.DoesNotContain(stats.TopLandedAirports, a => a.Icao == "EFHK");
    }

    [Fact]
    public void Calculate_TopRoutes_AreDirectionSensitive()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0)),
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 9, 0, 0)),
            Flight("ENGM", "EFHK", new DateOnly(2026, 1, 3), new DateTime(2026, 1, 3, 8, 0, 0), new DateTime(2026, 1, 3, 9, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        var efhkToEngm = stats.TopRoutes.Single(r => r.DepartureIcao == "EFHK" && r.ArrivalIcao == "ENGM");
        var engmToEfhk = stats.TopRoutes.Single(r => r.DepartureIcao == "ENGM" && r.ArrivalIcao == "EFHK");
        Assert.Equal(2, efhkToEngm.Count);
        Assert.Equal(1, engmToEfhk.Count);
    }

    [Fact]
    public void Calculate_LongestLegByTimeAndByDistance_CanPointAtDifferentFlights()
    {
        var flights = new[]
        {
            // Short hop, long block time (e.g. holding/taxi delays).
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 12, 0, 0)),
            // Long hop, short block time.
            Flight("EKCH", "LFPG", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 9, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        Assert.Equal("EFHK", stats.LongestLegByTime!.DepartureIcao);
        Assert.Equal("EKCH", stats.LongestLegByDistance!.Value.Flight.DepartureIcao);
    }

    [Fact]
    public void Calculate_UnknownAirport_ExcludedFromDistanceStatsButCountedElsewhere()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0)),
            Flight("EFHK", "ZZZZ", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 10, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        Assert.Equal(2, stats.TotalFlights);
        Assert.Equal(3, stats.VisitedAirportCount); // EFHK, ENGM, ZZZZ all counted for visits
        Assert.Equal(TimeSpan.FromHours(3), stats.TotalTime); // both legs' block time counted
        Assert.NotNull(stats.AverageLegDistanceNm); // only the resolvable leg contributes
        Assert.Equal("EFHK", stats.LongestLegByDistance!.Value.Flight.DepartureIcao);
        Assert.Equal("ENGM", stats.LongestLegByDistance!.Value.Flight.ArrivalIcao);
    }

    [Fact]
    public void Calculate_AverageAndTotalTime_AreComputedCorrectly()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0)),
            Flight("ENGM", "EFHK", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 11, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        Assert.Equal(TimeSpan.FromHours(4), stats.TotalTime);
        Assert.Equal(TimeSpan.FromHours(2), stats.AverageLegTime);
    }

    [Fact]
    public void Calculate_AircraftTypes_GroupedWithSummedTime()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0), "C172"),
            Flight("ENGM", "EFHK", new DateOnly(2026, 1, 2), new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 9, 0, 0), "C172"),
            Flight("EKCH", "LFPG", new DateOnly(2026, 1, 3), new DateTime(2026, 1, 3, 8, 0, 0), new DateTime(2026, 1, 3, 10, 0, 0), "A320"),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        var c172 = stats.AircraftTypes.Single(a => a.Model == "C172");
        var a320 = stats.AircraftTypes.Single(a => a.Model == "A320");
        Assert.Equal(2, c172.LegCount);
        Assert.Equal(TimeSpan.FromHours(2), c172.TotalTime);
        Assert.Equal(1, a320.LegCount);
        Assert.Equal(TimeSpan.FromHours(2), a320.TotalTime);
    }

    [Fact]
    public void Calculate_FirstAndLastFlightDate_SpanTheLogbook()
    {
        var flights = new[]
        {
            Flight("EFHK", "ENGM", new DateOnly(2026, 3, 15), new DateTime(2026, 3, 15, 8, 0, 0), new DateTime(2026, 3, 15, 9, 0, 0)),
            Flight("ENGM", "EFHK", new DateOnly(2026, 1, 1), new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 9, 0, 0)),
            Flight("EFHK", "EKCH", new DateOnly(2026, 6, 30), new DateTime(2026, 6, 30, 8, 0, 0), new DateTime(2026, 6, 30, 9, 0, 0)),
        };

        var stats = LogbookStatisticsService.Calculate(flights, CreateAirports());

        Assert.Equal(new DateOnly(2026, 1, 1), stats.FirstFlightDate);
        Assert.Equal(new DateOnly(2026, 6, 30), stats.LastFlightDate);
    }
}
