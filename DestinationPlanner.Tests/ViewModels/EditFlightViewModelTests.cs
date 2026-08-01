using System.Globalization;
using DestinationPlanner.Models;
using DestinationPlanner.ViewModels;

namespace DestinationPlanner.Tests.ViewModels;

public class EditFlightViewModelTests
{
    private static FlightRecord CreateRecordWithLandingStats() => new()
    {
        Date          = new DateOnly(2026, 1, 1),
        AircraftModel = "C172",
        DepartureIcao = "EFHK",
        ArrivalIcao   = "ENGM",
        BlockOffUtc   = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
        BlockOnUtc    = new DateTime(2026, 1, 1, 9, 30, 0, DateTimeKind.Utc),
        LandingFpm                   = -180,
        LandingGForce                = 1.3,
        LandingAirspeedKts           = 65,
        LandingWindKts               = 8,
        LandingWindDirection         = 270,
        LandingHeadingDeg            = 90,
        LandingBankAngleDeg          = 2,
        LandingPitchAngleDeg         = 5,
        LandingCrosswindKts          = 4,
        LandingCenterlineDeviationFt = 3,
        LandingTouchdownZonePct      = 40,
        LandingStars                 = 4,
    };

    [Fact]
    public void RoundTrip_PreservesLandingStats_WhenAllSet()
    {
        var original = CreateRecordWithLandingStats();
        var vm = EditFlightViewModel.FromRecord(original);

        var updated = vm.ToRecord(original.Id);

        Assert.Equal(original.LandingFpm, updated.LandingFpm);
        Assert.Equal(original.LandingGForce, updated.LandingGForce);
        Assert.Equal(original.LandingAirspeedKts, updated.LandingAirspeedKts);
        Assert.Equal(original.LandingWindKts, updated.LandingWindKts);
        Assert.Equal(original.LandingWindDirection, updated.LandingWindDirection);
        Assert.Equal(original.LandingHeadingDeg, updated.LandingHeadingDeg);
        Assert.Equal(original.LandingBankAngleDeg, updated.LandingBankAngleDeg);
        Assert.Equal(original.LandingPitchAngleDeg, updated.LandingPitchAngleDeg);
        Assert.Equal(original.LandingCrosswindKts, updated.LandingCrosswindKts);
        Assert.Equal(original.LandingCenterlineDeviationFt, updated.LandingCenterlineDeviationFt);
        Assert.Equal(original.LandingTouchdownZonePct, updated.LandingTouchdownZonePct);
        Assert.Equal(original.LandingStars, updated.LandingStars);
    }

    [Fact]
    public void RoundTrip_PreservesLandingStats_WhenAllNull()
    {
        var original = new FlightRecord
        {
            Date          = new DateOnly(2026, 1, 1),
            AircraftModel = "C172",
            DepartureIcao = "EFHK",
            ArrivalIcao   = "ENGM",
            BlockOffUtc   = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc),
            BlockOnUtc    = new DateTime(2026, 1, 1, 9, 30, 0, DateTimeKind.Utc),
        };
        var vm = EditFlightViewModel.FromRecord(original);

        var updated = vm.ToRecord(original.Id);

        Assert.Null(updated.LandingFpm);
        Assert.Null(updated.LandingGForce);
        Assert.Null(updated.LandingAirspeedKts);
        Assert.Null(updated.LandingWindKts);
        Assert.Null(updated.LandingWindDirection);
        Assert.Null(updated.LandingHeadingDeg);
        Assert.Null(updated.LandingBankAngleDeg);
        Assert.Null(updated.LandingPitchAngleDeg);
        Assert.Null(updated.LandingCrosswindKts);
        Assert.Null(updated.LandingCenterlineDeviationFt);
        Assert.Null(updated.LandingTouchdownZonePct);
        Assert.Null(updated.LandingStars);
    }

    [Fact]
    public void ToRecord_ReflectsEditedFields_NotJustOriginalValues()
    {
        var original = CreateRecordWithLandingStats();
        var vm = EditFlightViewModel.FromRecord(original);

        vm.AircraftModel = "A320";
        vm.DepartureIcao = "ekch";
        vm.ArrivalIcao   = "lfpg";
        vm.BlockOffDate  = new DateTime(2026, 2, 2);
        vm.BlockOffTime  = "10:15";
        vm.BlockOnDate   = new DateTime(2026, 2, 2);
        vm.BlockOnTime   = "12:00";

        var updated = vm.ToRecord(original.Id);

        Assert.Equal("A320", updated.AircraftModel);
        Assert.Equal("EKCH", updated.DepartureIcao);
        Assert.Equal("LFPG", updated.ArrivalIcao);
        Assert.Equal(new DateTime(2026, 2, 2, 10, 15, 0, DateTimeKind.Utc), updated.BlockOffUtc);
        Assert.Equal(new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc), updated.BlockOnUtc);
        Assert.Equal(new DateOnly(2026, 2, 2), updated.Date);
        // Landing stats still carried through even though other fields changed.
        Assert.Equal(original.LandingStars, updated.LandingStars);
    }

    private static EditFlightViewModel CreateValidViewModel() => EditFlightViewModel.FromRecord(CreateRecordWithLandingStats());

    [Fact]
    public void ValidationMessage_Empty_WhenAllFieldsValid()
    {
        var vm = CreateValidViewModel();

        Assert.Equal(string.Empty, vm.ValidationMessage);
        Assert.True(vm.IsValid);
    }

    [Fact]
    public void ValidationMessage_ReportsMissingBlockOffDate()
    {
        var vm = CreateValidViewModel();
        vm.BlockOffDate = null;

        Assert.Equal("Block Off date is required.", vm.ValidationMessage);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void ValidationMessage_ReportsUnparseableBlockOffTime()
    {
        var vm = CreateValidViewModel();
        vm.BlockOffTime = "0930";

        Assert.Equal("Block Off time must be in h:mm or hh:mm format (e.g. 9:30).", vm.ValidationMessage);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void ValidationMessage_ReportsMissingBlockOnDate()
    {
        var vm = CreateValidViewModel();
        vm.BlockOnDate = null;

        Assert.Equal("Block On date is required.", vm.ValidationMessage);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void ValidationMessage_ReportsUnparseableBlockOnTime()
    {
        var vm = CreateValidViewModel();
        vm.BlockOnTime = "not-a-time";

        Assert.Equal("Block On time must be in h:mm or hh:mm format (e.g. 9:30).", vm.ValidationMessage);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void ValidationMessage_ReportsMissingDepartureIcao()
    {
        var vm = CreateValidViewModel();
        vm.DepartureIcao = "  ";

        Assert.Equal("Departure ICAO is required.", vm.ValidationMessage);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void ValidationMessage_ReportsMissingArrivalIcao()
    {
        var vm = CreateValidViewModel();
        vm.ArrivalIcao = "";

        Assert.Equal("Arrival ICAO is required.", vm.ValidationMessage);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void FromRecord_ProducesAValidTime_RegardlessOfCurrentCulture()
    {
        // DateTime.ToString("HH:mm") treats an unescaped ':' as the CURRENT CULTURE's time
        // separator (e.g. "." in fi-FI), not a literal colon — while TryParseTime requires a
        // literal colon. FromRecord must escape it so the dialog opens already valid on any
        // machine locale, regardless of what culture is active when a flight is loaded for editing.
        var original = new CultureInfo("fi-FI");
        var previous = CultureInfo.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = original;
        try
        {
            var record = CreateRecordWithLandingStats();
            var vm = EditFlightViewModel.FromRecord(record);

            Assert.Equal(string.Empty, vm.ValidationMessage);
            Assert.True(vm.IsValid);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
