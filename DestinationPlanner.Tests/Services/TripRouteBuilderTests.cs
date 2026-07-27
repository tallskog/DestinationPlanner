using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Services;

public class TripRouteBuilderTests
{
    // Roughly evenly spaced along a line of longitude at 60N so consecutive-airport distances
    // are predictable and increase monotonically with index — easy to reason about in tests.
    // Approx spacing at 60N: ~1 degree of longitude ~= 30nm, so each step below is ~90nm.
    private static readonly Airport A = new() { Icao = "AAAA", Latitude = 60.0, Longitude = 0.0 };
    private static readonly Airport B = new() { Icao = "BBBB", Latitude = 60.0, Longitude = 3.0 };
    private static readonly Airport C = new() { Icao = "CCCC", Latitude = 60.0, Longitude = 6.0 };
    private static readonly Airport D = new() { Icao = "DDDD", Latitude = 60.0, Longitude = 9.0 };

    [Fact]
    public void BuildRoute_FewerThanTwoCandidates_ReturnsEmpty()
    {
        var route = TripRouteBuilder.BuildRoute([A], minNm: 0, maxNm: 1000, startIcao: null);

        Assert.Empty(route);
    }

    [Fact]
    public void BuildRoute_AllWithinBounds_ConnectsEveryCandidate()
    {
        var route = TripRouteBuilder.BuildRoute([A, B, C, D], minNm: 0, maxNm: 1000, startIcao: "AAAA");

        Assert.Equal(3, route.Count);
        Assert.Equal("AAAA", route[0].From.Icao);
        Assert.Equal("BBBB", route[0].To.Icao);
        Assert.Equal("BBBB", route[1].From.Icao);
        Assert.Equal("CCCC", route[1].To.Icao);
        Assert.Equal("CCCC", route[2].From.Icao);
        Assert.Equal("DDDD", route[2].To.Icao);
    }

    [Fact]
    public void BuildRoute_EveryLegRespectsDistanceBounds()
    {
        var route = TripRouteBuilder.BuildRoute([A, B, C, D], minNm: 0, maxNm: 1000, startIcao: "AAAA");

        Assert.All(route, leg =>
        {
            var actual = GeoHelper.DistanceNm(leg.From.Latitude, leg.From.Longitude, leg.To.Latitude, leg.To.Longitude);
            Assert.Equal(actual, leg.DistanceNm, precision: 6);
            Assert.InRange(leg.DistanceNm, 0, 1000);
        });
    }

    [Fact]
    public void BuildRoute_HonorsStartIcao()
    {
        var route = TripRouteBuilder.BuildRoute([A, B, C, D], minNm: 0, maxNm: 1000, startIcao: "CCCC");

        Assert.Equal("CCCC", route[0].From.Icao);
    }

    [Fact]
    public void BuildRoute_UnknownStartIcao_FallsBackToFirstCandidate()
    {
        var route = TripRouteBuilder.BuildRoute([A, B, C, D], minNm: 0, maxNm: 1000, startIcao: "NOPE");

        Assert.Equal("AAAA", route[0].From.Icao);
    }

    [Fact]
    public void BuildRoute_NoNeighborWithinBounds_StopsRatherThanViolatingConstraint()
    {
        // A-B is ~90nm; force a tight max that A-B satisfies but nothing further does.
        double abDistance = GeoHelper.DistanceNm(A.Latitude, A.Longitude, B.Latitude, B.Longitude);
        var route = TripRouteBuilder.BuildRoute([A, B, C, D], minNm: 0, maxNm: abDistance + 1, startIcao: "AAAA");

        // Only the first hop can possibly be within range; every leg must still respect maxNm.
        Assert.All(route, leg => Assert.True(leg.DistanceNm <= abDistance + 1));
    }

    [Fact]
    public void BuildRoute_MinimumBoundExcludesTooCloseCandidates()
    {
        double abDistance = GeoHelper.DistanceNm(A.Latitude, A.Longitude, B.Latitude, B.Longitude);

        // Minimum just above A-B distance means B can't be the first hop from A — C should be
        // chosen instead (assuming A-C satisfies the minimum, which it does at double the spacing).
        var route = TripRouteBuilder.BuildRoute([A, B, C], minNm: abDistance + 1, maxNm: 1000, startIcao: "AAAA");

        Assert.NotEmpty(route);
        Assert.NotEqual("BBBB", route[0].To.Icao);
    }

    [Fact]
    public void BuildRoute_PicksNearestValidNeighborNotJustFirst()
    {
        // From A, both C and D are within range, but C is nearer — the greedy walk should pick it.
        var route = TripRouteBuilder.BuildRoute([A, C, D], minNm: 0, maxNm: 1000, startIcao: "AAAA");

        Assert.Equal("CCCC", route[0].To.Icao);
    }
}
