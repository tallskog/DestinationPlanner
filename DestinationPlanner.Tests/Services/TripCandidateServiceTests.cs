using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;

namespace DestinationPlanner.Tests.Services;

public class TripCandidateServiceTests
{
    private static FakeAirportDataService CreateAirports() => new(new[]
    {
        new Airport { Icao = "EFHK", Name = "Helsinki",  Latitude = 60.3, Longitude = 24.9, LongestRunwayFt = 10000 },
        new Airport { Icao = "ENGM", Name = "Oslo",      Latitude = 60.2, Longitude = 11.1, LongestRunwayFt = 9700 },
        new Airport { Icao = "EKCH", Name = "Copenhagen",Latitude = 55.6, Longitude = 12.6, LongestRunwayFt = 11800 },
        new Airport { Icao = "LFPG", Name = "Paris CDG", Latitude = 49.0, Longitude = 2.5,  LongestRunwayFt = 13800 },
    });

    [Fact]
    public void GetCandidates_NoRestriction_ReturnsAllAirports()
    {
        var service = new TripCandidateService(CreateAirports(), new FakeLogbookService());

        var result = service.GetCandidates(new AirportFilterCriteria(), excludeVisited: false);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void GetCandidates_MinRunwayAndPrefix_AppliesBothFilters()
    {
        var service = new TripCandidateService(CreateAirports(), new FakeLogbookService());
        var criteria = new AirportFilterCriteria { MinRunwayFt = 9000, IcaoPrefixes = ["EN", "EF", "EK"] };

        var result = service.GetCandidates(criteria, excludeVisited: false);

        Assert.Equal(["EFHK", "EKCH", "ENGM"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void GetCandidates_ExcludeVisited_RemovesLoggedAirports()
    {
        var logbook = new FakeLogbookService();
        logbook.SetFlights([new FlightRecord { DepartureIcao = "EFHK", ArrivalIcao = "ENGM" }]);
        var service = new TripCandidateService(CreateAirports(), logbook);

        var result = service.GetCandidates(new AirportFilterCriteria(), excludeVisited: true);

        Assert.Equal(["EKCH", "LFPG"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void GetCandidates_ExcludeVisitedFalse_KeepsVisitedAirports()
    {
        var logbook = new FakeLogbookService();
        logbook.SetFlights([new FlightRecord { DepartureIcao = "EFHK", ArrivalIcao = "ENGM" }]);
        var service = new TripCandidateService(CreateAirports(), logbook);

        var result = service.GetCandidates(new AirportFilterCriteria(), excludeVisited: false);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void GetCandidates_CenterRadius_UnknownCenterIcao_ReturnsEmpty()
    {
        var service = new TripCandidateService(CreateAirports(), new FakeLogbookService());
        var criteria = new AirportFilterCriteria { FilterCenterIcao = "NOPE", FilterRadiusNm = 200 };

        var result = service.GetCandidates(criteria, excludeVisited: false);

        Assert.Empty(result);
    }

    [Fact]
    public void GetCandidates_AirportDataNotLoaded_ReturnsEmpty()
    {
        var airports = CreateAirports();
        airports.IsLoaded = false;
        var service = new TripCandidateService(airports, new FakeLogbookService());

        var result = service.GetCandidates(new AirportFilterCriteria(), excludeVisited: false);

        Assert.Empty(result);
    }
}
