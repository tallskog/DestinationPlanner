using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;

namespace DestinationPlanner.Tests.Services;

public class AirportFilterServiceTests
{
    private static FakeAirportDataService CreateAirports() => new(new[]
    {
        new Airport { Icao = "EFHK", Name = "Civil Field",        Latitude = 60.0, Longitude = 24.0,  LongestRunwayFt = 10000, Type = AirportType.Civil,        HasInstrumentApproach = true,  HasAtis = true },
        new Airport { Icao = "EFTU", Name = "Military Field",     Latitude = 60.5, Longitude = 23.0,  LongestRunwayFt = 8000,  Type = AirportType.Military,     HasInstrumentApproach = false, HasAtis = false },
        new Airport { Icao = "EFHF", Name = "Heliport Field",     Latitude = 60.2, Longitude = 24.9,  LongestRunwayFt = 2000,  Type = AirportType.Heliport,     HasInstrumentApproach = false, HasAtis = false },
        new Airport { Icao = "EGLL", Name = "Private Field",      Latitude = 51.4, Longitude = -0.4,  LongestRunwayFt = 12000, Type = AirportType.Private,      HasInstrumentApproach = true,  HasAtis = true },
        new Airport { Icao = "LFLJ", Name = "Other Field",        Latitude = 45.4, Longitude = 6.6,   LongestRunwayFt = 5000,  Type = AirportType.Other,        HasInstrumentApproach = false, HasAtis = false },
        new Airport { Icao = "WWWW", Name = "Unknown Field",      Latitude = 5.0,  Longitude = 100.0, LongestRunwayFt = 6000,  Type = AirportType.Unknown,      HasInstrumentApproach = false, HasAtis = false },
        new Airport { Icao = "ZZZZ", Name = "Unclassified Field", Latitude = 10.0, Longitude = 20.0,  LongestRunwayFt = 7000,  Type = AirportType.Unclassified, HasInstrumentApproach = false, HasAtis = false },
    });

    [Fact]
    public void ApplyRunwayTypeAndPrefixFilters_DefaultCriteria_ReturnsAllAirports()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(airports.GetAll(), new AirportFilterCriteria());

        Assert.Equal(7, result.Count());
    }

    [Fact]
    public void ApplyRunwayTypeAndPrefixFilters_MinRunwayFt_ExcludesShorterRunways()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(
            airports.GetAll(), new AirportFilterCriteria { MinRunwayFt = 9000 });

        Assert.Equal(["EFHK", "EGLL"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void ApplyRunwayTypeAndPrefixFilters_MaxRunwayFt_ExcludesLongerRunways()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(
            airports.GetAll(), new AirportFilterCriteria { MaxRunwayFt = 6000 });

        Assert.Equal(["EFHF", "LFLJ", "WWWW"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void ApplyRunwayTypeAndPrefixFilters_RequireInstrumentApproach_KeepsOnlyMatching()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(
            airports.GetAll(), new AirportFilterCriteria { RequireInstrumentApproach = true });

        Assert.Equal(["EFHK", "EGLL"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void ApplyRunwayTypeAndPrefixFilters_RequireAtis_KeepsOnlyMatching()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(
            airports.GetAll(), new AirportFilterCriteria { RequireAtis = true });

        Assert.Equal(["EFHK", "EGLL"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Theory]
    [InlineData(false, true,  true,  true,  true,  true,  true,  "EFHK")]  // civil excluded
    [InlineData(true,  false, true,  true,  true,  true,  true,  "EFTU")]  // military excluded
    [InlineData(true,  true,  false, true,  true,  true,  true,  "EFHF")]  // heliport excluded
    [InlineData(true,  true,  true,  false, true,  true,  true,  "EGLL")]  // private excluded
    [InlineData(true,  true,  true,  true,  false, true,  true,  "LFLJ")]  // other excluded
    [InlineData(true,  true,  true,  true,  true,  false, true,  "WWWW")]  // unknown excluded
    [InlineData(true,  true,  true,  true,  true,  true,  false, "ZZZZ")]  // unclassified excluded
    public void ApplyRunwayTypeAndPrefixFilters_AirportTypeUnchecked_ExcludesOnlyThatType(
        bool civil, bool military, bool heliport, bool priv, bool other, bool unknown, bool unclassified, string excludedIcao)
    {
        var airports = CreateAirports();
        var criteria = new AirportFilterCriteria
        {
            ShowCivilAirports = civil,
            ShowMilitaryAirports = military,
            ShowHeliportAirports = heliport,
            ShowPrivateAirports = priv,
            ShowOtherAirports = other,
            ShowUnknownAirports = unknown,
            ShowUnclassifiedAirports = unclassified,
        };

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(airports.GetAll(), criteria);

        Assert.DoesNotContain(result, a => a.Icao == excludedIcao);
        Assert.Equal(6, result.Count());
    }

    [Fact]
    public void ApplyRunwayTypeAndPrefixFilters_IcaoPrefixes_KeepsOnlyMatchingPrefixes()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(
            airports.GetAll(), new AirportFilterCriteria { IcaoPrefixes = ["EF"] });

        Assert.Equal(["EFHF", "EFHK", "EFTU"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void ResolveIcaoOverrides_FourCharacterCode_ReturnsMatchingAirport()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ResolveIcaoOverrides(airports, ["EFHK"]);

        Assert.Equal(["EFHK"], result.Select(a => a.Icao));
    }

    [Fact]
    public void ResolveIcaoOverrides_IgnoresEntriesNotExactlyFourCharacters()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ResolveIcaoOverrides(airports, ["EF", "EFHK12"]);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveIcaoOverrides_UnknownFourCharacterCode_IsSkippedNotThrown()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ResolveIcaoOverrides(airports, ["NOPE"]);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolveIcaoOverrides_MultipleCodes_ReturnsAllMatches()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ResolveIcaoOverrides(airports, ["EFHK", "EGLL"]);

        Assert.Equal(["EFHK", "EGLL"], result.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void ApplyCenterRadius_FromAirportDataService_ReturnsAirportsWithinRadius()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyCenterRadius(airports, "EFHK", 100);

        Assert.NotNull(result);
        Assert.Contains(result!, a => a.Icao == "EFHK");
        Assert.Contains(result!, a => a.Icao == "EFTU");
        Assert.DoesNotContain(result!, a => a.Icao == "EGLL");
    }

    [Fact]
    public void ApplyCenterRadius_FromAirportDataService_UnknownCenterIcao_ReturnsNull()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyCenterRadius(airports, "NOPE", 100);

        Assert.Null(result);
    }

    [Fact]
    public void ApplyCenterRadius_OnGivenCandidates_NarrowsByDistance()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyCenterRadius(airports.GetAll(), airports, "EFHK", 100);

        Assert.NotNull(result);
        Assert.Contains(result!, a => a.Icao == "EFTU");
        Assert.DoesNotContain(result!, a => a.Icao == "EGLL");
    }

    [Fact]
    public void ApplyCenterRadius_OnGivenCandidates_UnknownCenterIcao_ReturnsNull()
    {
        var airports = CreateAirports();

        var result = AirportFilterService.ApplyCenterRadius(airports.GetAll(), airports, "NOPE", 100);

        Assert.Null(result);
    }
}
