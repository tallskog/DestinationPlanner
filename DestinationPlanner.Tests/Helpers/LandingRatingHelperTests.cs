using DestinationPlanner.Helpers;
using DestinationPlanner.Models;

namespace DestinationPlanner.Tests.Helpers;

public class LandingRatingHelperTests
{
    [Fact]
    public void ComputeBreakdown_NoLandingData_ReturnsNull()
    {
        var record = new FlightRecord();

        Assert.Null(LandingRatingHelper.ComputeBreakdown(record));
        Assert.Null(LandingRatingHelper.ComputeStars(record));
    }

    // Isolating a single populated component makes the overall (weighted-average) score
    // exactly equal to that component's own score, since it's the only weight in play.

    [Fact]
    public void ComputeBreakdown_GentleFpmOnly_ScoresPerfectAndFiveStars()
    {
        var record = new FlightRecord { LandingFpm = -50 }; // |−50| <= 100 -> flat 100 region

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.NotNull(breakdown);
        Assert.Equal(100, breakdown!.TotalScore, precision: 6);
        Assert.Equal(5, breakdown.Stars);
    }

    [Fact]
    public void ComputeBreakdown_HardFpmOnly_ScoresZeroAndOneStar()
    {
        var record = new FlightRecord { LandingFpm = -600 }; // |−600| > 500 -> flat 0 region

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.NotNull(breakdown);
        Assert.Equal(0, breakdown!.TotalScore, precision: 6);
        Assert.Equal(1, breakdown.Stars);
    }

    [Fact]
    public void ComputeBreakdown_FpmInLerpRange_InterpolatesLinearly()
    {
        // ScoreFpm(150) should linearly interpolate between (100 -> 100) and (200 -> 80).
        var record = new FlightRecord { LandingFpm = -150 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(90, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_SmoothGForceOnly_ScoresPerfect()
    {
        var record = new FlightRecord { LandingGForce = 1.0 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(100, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_HardGForceOnly_ScoresZero()
    {
        var record = new FlightRecord { LandingGForce = 2.5 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(0, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_WingsLevelBankOnly_ScoresPerfect()
    {
        var record = new FlightRecord { LandingBankAngleDeg = 1 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(100, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_SteepBankOnly_ScoresZero()
    {
        var record = new FlightRecord { LandingBankAngleDeg = -15 }; // abs 15 > 10

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(0, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_NoseUpFlarePitchOnly_ScoresPerfect()
    {
        var record = new FlightRecord { LandingPitchAngleDeg = 3 }; // within [0,5]

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(100, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_ExtremeNoseDownPitchOnly_ScoresZero()
    {
        var record = new FlightRecord { LandingPitchAngleDeg = -10 }; // < -2 -> flat 0

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(0, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_OnCenterlineOnly_ScoresPerfect()
    {
        var record = new FlightRecord { LandingCenterlineDeviationFt = 2 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(100, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_FarOffCenterlineOnly_ScoresZero()
    {
        var record = new FlightRecord { LandingCenterlineDeviationFt = 150 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(0, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_FirstThirdTouchdownZoneOnly_ScoresPerfect()
    {
        var record = new FlightRecord { LandingTouchdownZonePct = 10 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(100, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_InvalidTouchdownZonePercentOnly_ScoresZero()
    {
        var record = new FlightRecord { LandingTouchdownZonePct = 150 }; // out of [0,100]

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        Assert.Equal(0, breakdown!.TotalScore, precision: 6);
    }

    [Fact]
    public void ComputeBreakdown_MissingComponentsShowDashInMeasuredValue()
    {
        var record = new FlightRecord { LandingFpm = -50 };

        var breakdown = LandingRatingHelper.ComputeBreakdown(record);

        var gForceComponent = breakdown!.Components.Single(c => c.Name == "G-Force");
        Assert.Equal("—", gForceComponent.MeasuredValue);
        Assert.Null(gForceComponent.Score);
    }

    [Fact]
    public void Enrich_ComputesSignedCrosswindComponent()
    {
        // Wind from 90° (right of nose) relative to a 0° heading -> positive (from the right).
        var record = new FlightRecord
        {
            LandingWindKts = 20,
            LandingWindDirection = 90,
            LandingHeadingDeg = 0,
        };

        LandingRatingHelper.Enrich(record, touchdownLat: 60.0, touchdownLon: 24.0, runways: null);

        Assert.NotNull(record.LandingCrosswindKts);
        Assert.Equal(20, record.LandingCrosswindKts!.Value, precision: 6);
    }

    [Fact]
    public void Enrich_NoWindData_LeavesCrosswindNull()
    {
        var record = new FlightRecord();

        LandingRatingHelper.Enrich(record, touchdownLat: 60.0, touchdownLon: 24.0, runways: null);

        Assert.Null(record.LandingCrosswindKts);
    }

    [Fact]
    public void Enrich_TouchdownOnCenterlineNearThreshold_ComputesLowDeviationAndEarlyTouchdownZone()
    {
        // A runway running due north (LE heading 0) roughly 1nm long, threshold near (60.0, 24.0).
        var runway = new Runway
        {
            Ident = "01/19",
            LengthFt = 6000,
            LeLatitude = 60.0,
            LeLongitude = 24.0,
            LeHeadingDeg = 0,
            HeLatitude = 60.0166, // ~1nm further north
            HeLongitude = 24.0,
        };
        // Touchdown a short distance down the centerline from the threshold, heading matches LE.
        var record = new FlightRecord { LandingHeadingDeg = 0 };

        LandingRatingHelper.Enrich(record, touchdownLat: 60.002, touchdownLon: 24.0, runways: [runway]);

        Assert.NotNull(record.LandingCenterlineDeviationFt);
        Assert.InRange(record.LandingCenterlineDeviationFt!.Value, 0, 5); // essentially on the centerline
        Assert.NotNull(record.LandingTouchdownZonePct);
        Assert.InRange(record.LandingTouchdownZonePct!.Value, 0, 33); // near the threshold -> first third
    }

    [Fact]
    public void Enrich_SetsLandingStarsFromComputedBreakdown()
    {
        var record = new FlightRecord { LandingFpm = -50 };

        LandingRatingHelper.Enrich(record, touchdownLat: 60.0, touchdownLon: 24.0, runways: null);

        Assert.Equal(5, record.LandingStars);
    }
}
