using DestinationPlanner.Models;

namespace DestinationPlanner.Tests.Models;

public class TripQueryFiltersTests
{
    [Fact]
    public void ToFilterCriteria_DefaultFilters_IncludesEveryAirportType()
    {
        var filters = new TripQueryFilters();

        var criteria = filters.ToFilterCriteria();

        Assert.True(criteria.ShowCivilAirports);
        Assert.True(criteria.ShowMilitaryAirports);
        Assert.True(criteria.ShowHeliportAirports);
        Assert.True(criteria.ShowPrivateAirports);
        Assert.True(criteria.ShowOtherAirports);
        Assert.True(criteria.ShowUnknownAirports);
        Assert.True(criteria.ShowUnclassifiedAirports);
    }

    // Guards the exact gap reported by the user: "no military airports" in a natural-language
    // query must actually restrict the resulting AirportFilterCriteria, not just be echoed back
    // in IntentSummary while every type stays included.
    [Fact]
    public void ToFilterCriteria_MilitaryExcluded_CarriesThroughToCriteria()
    {
        var filters = new TripQueryFilters { ShowMilitaryAirports = false };

        var criteria = filters.ToFilterCriteria();

        Assert.False(criteria.ShowMilitaryAirports);
        Assert.True(criteria.ShowCivilAirports);
    }

    // The second field (alongside airport type) that TripQueryFilters was completely missing
    // until reported — RequireInstrumentApproach/RequireAtis had no schema field at all, so
    // "airports with ILS" had nowhere to land regardless of intentSummary wording.
    [Fact]
    public void ToFilterCriteria_InstrumentApproachAndAtis_CarryThroughToCriteria()
    {
        var filters = new TripQueryFilters { RequireInstrumentApproach = true, RequireAtis = true };

        var criteria = filters.ToFilterCriteria();

        Assert.True(criteria.RequireInstrumentApproach);
        Assert.True(criteria.RequireAtis);
    }

    [Fact]
    public void ToFilterCriteria_DefaultFilters_DoesNotRequireInstrumentApproachOrAtis()
    {
        var filters = new TripQueryFilters();

        var criteria = filters.ToFilterCriteria();

        Assert.False(criteria.RequireInstrumentApproach);
        Assert.False(criteria.RequireAtis);
    }

    [Fact]
    public void ToFilterCriteria_OnlyCivilAllowed_ExcludesEveryOtherType()
    {
        var filters = new TripQueryFilters
        {
            ShowCivilAirports = true,
            ShowMilitaryAirports = false,
            ShowHeliportAirports = false,
            ShowPrivateAirports = false,
            ShowOtherAirports = false,
            ShowUnknownAirports = false,
            ShowUnclassifiedAirports = false,
        };

        var criteria = filters.ToFilterCriteria();

        Assert.True(criteria.ShowCivilAirports);
        Assert.False(criteria.ShowMilitaryAirports);
        Assert.False(criteria.ShowHeliportAirports);
        Assert.False(criteria.ShowPrivateAirports);
        Assert.False(criteria.ShowOtherAirports);
        Assert.False(criteria.ShowUnknownAirports);
        Assert.False(criteria.ShowUnclassifiedAirports);
    }
}
