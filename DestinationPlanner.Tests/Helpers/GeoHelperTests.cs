using DestinationPlanner.Helpers;

namespace DestinationPlanner.Tests.Helpers;

public class GeoHelperTests
{
    [Fact]
    public void DistanceNm_SamePoint_ReturnsZero()
    {
        Assert.Equal(0, GeoHelper.DistanceNm(60.3, 24.9, 60.3, 24.9), precision: 6);
    }

    [Fact]
    public void DistanceNm_OneDegreeApart_IsApproximatelySixtyNm()
    {
        // 1 nm was historically defined as 1 minute of arc of latitude, so 1 degree ~ 60 nm.
        double lonDelta = GeoHelper.DistanceNm(0, 0, 0, 1);
        double latDelta = GeoHelper.DistanceNm(0, 0, 1, 0);

        Assert.InRange(lonDelta, 59.5, 60.5);
        Assert.InRange(latDelta, 59.5, 60.5);
    }

    [Fact]
    public void DistanceNm_IsSymmetric()
    {
        double ab = GeoHelper.DistanceNm(60.3172, 24.9633, 51.4706, -0.4619);
        double ba = GeoHelper.DistanceNm(51.4706, -0.4619, 60.3172, 24.9633);

        Assert.Equal(ab, ba, precision: 9);
    }

    [Theory]
    [InlineData(1000, 305)]  // 1000 * 0.3048 = 304.8 -> rounds to 305
    [InlineData(0, 0)]
    [InlineData(1, 0)]       // 0.3048 rounds down to 0
    public void FeetToMeters_RoundsToNearestMeter(int feet, int expectedMeters)
    {
        Assert.Equal(expectedMeters, GeoHelper.FeetToMeters(feet));
    }

    [Theory]
    [InlineData(1000, 3281)] // 1000 / 0.3048 = 3280.84 -> rounds to 3281
    [InlineData(0, 0)]
    public void MetersToFeet_RoundsToNearestFoot(int meters, int expectedFeet)
    {
        Assert.Equal(expectedFeet, GeoHelper.MetersToFeet(meters));
    }

    [Fact]
    public void MercatorRoundTrip_LonAndLat_RecoversOriginalWithinTolerance()
    {
        const double lon = 24.9633;
        const double lat = 60.3172;

        double x = GeoHelper.LonToMercatorX(lon);
        double y = GeoHelper.LatToMercatorY(lat);

        Assert.Equal(lon, GeoHelper.MercatorXToLon(x), precision: 6);
        Assert.Equal(lat, GeoHelper.MercatorYToLat(y), precision: 6);
    }

    [Fact]
    public void LonToMercatorX_Zero_IsZero()
    {
        Assert.Equal(0, GeoHelper.LonToMercatorX(0), precision: 9);
    }

    [Fact]
    public void LatToMercatorY_Zero_IsZero()
    {
        Assert.Equal(0, GeoHelper.LatToMercatorY(0), precision: 6);
    }

    [Fact]
    public void LonToMercatorX_At180Degrees_MatchesStandardWebMercatorBound()
    {
        // Well-known EPSG:3857 max-X constant (R * pi).
        Assert.Equal(20037508.34, GeoHelper.LonToMercatorX(180), precision: 1);
    }
}
