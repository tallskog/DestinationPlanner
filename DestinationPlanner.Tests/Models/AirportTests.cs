using DestinationPlanner.Models;

namespace DestinationPlanner.Tests.Models;

public class AirportTests
{
    private static Airport CreateBaseline() => new()
    {
        Icao = "EFHK",
        Name = "Helsinki-Vantaa",
        Type = AirportType.Civil,
        HasAtis = true,
        HasInstrumentApproach = true,
        LongestRunwayFt = 11000,
    };

    [Fact]
    public void EffectiveProperties_NoOverride_ReturnBaselineValues()
    {
        var airport = CreateBaseline();

        Assert.Equal(AirportType.Civil, airport.EffectiveType);
        Assert.Equal("Helsinki-Vantaa", airport.EffectiveName);
        Assert.True(airport.EffectiveHasAtis);
        Assert.True(airport.EffectiveHasInstrumentApproach);
        Assert.Equal(11000, airport.EffectiveLongestRunwayFt);
        Assert.False(airport.IsHidden);
    }

    [Fact]
    public void EffectiveProperties_PartialOverride_OnlyOverriddenFieldsChange()
    {
        var airport = CreateBaseline();
        airport.Override = new AirportOverride { Type = AirportType.Military };

        Assert.Equal(AirportType.Military, airport.EffectiveType);
        Assert.Equal("Helsinki-Vantaa", airport.EffectiveName); // untouched
        Assert.True(airport.EffectiveHasAtis);                  // untouched
        Assert.Equal(11000, airport.EffectiveLongestRunwayFt);  // untouched
    }

    [Fact]
    public void EffectiveProperties_FullOverride_EveryFieldReflectsTheOverride()
    {
        var airport = CreateBaseline();
        airport.Override = new AirportOverride
        {
            Type = AirportType.Private,
            Name = "Corrected Name",
            HasAtis = false,
            HasInstrumentApproach = false,
            LongestRunwayFt = 5000,
            IsHidden = true,
        };

        Assert.Equal(AirportType.Private, airport.EffectiveType);
        Assert.Equal("Corrected Name", airport.EffectiveName);
        Assert.False(airport.EffectiveHasAtis);
        Assert.False(airport.EffectiveHasInstrumentApproach);
        Assert.Equal(5000, airport.EffectiveLongestRunwayFt);
        Assert.True(airport.IsHidden);
    }

    [Fact]
    public void IsHidden_OverrideExistsButHiddenNotSet_DefaultsToFalse()
    {
        var airport = CreateBaseline();
        airport.Override = new AirportOverride { Type = AirportType.Military };

        Assert.False(airport.IsHidden);
    }
}
