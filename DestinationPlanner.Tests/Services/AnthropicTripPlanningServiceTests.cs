using DestinationPlanner.Models;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Services;

// Covers only the pure prompt-building and JSON-response-parsing helpers — no network call.
// The live Claude API call is verified manually (see CLAUDE.md Testing section), the same
// carve-out this codebase already uses for SimConnect/live-MSFS behavior.
public class AnthropicTripPlanningServiceTests
{
    [Fact]
    public void BuildParseQueryRequest_TargetsSonnet5()
    {
        var request = AnthropicTripPlanningService.BuildParseQueryRequest("airports in the nordics");

        Assert.Equal("claude-sonnet-5", request.Model.ToString().Trim('"'));
    }

    [Fact]
    public void BuildPlanTripRequest_TargetsSonnet5()
    {
        var candidates = new List<Airport> { new() { Icao = "EFHK" }, new() { Icao = "ENGM" } };

        var request = AnthropicTripPlanningService.BuildPlanTripRequest(candidates, "plan a trip", startIcao: null);

        Assert.Equal("claude-sonnet-5", request.Model.ToString().Trim('"'));
    }

    [Fact]
    public void BuildPlanTripRequest_EmptyCandidateList_DoesNotThrow()
    {
        var request = AnthropicTripPlanningService.BuildPlanTripRequest([], "plan a trip", startIcao: null);

        Assert.NotNull(request);
    }

    [Fact]
    public void ParseQueryFiltersResponse_ValidJson_ReturnsParsedFilters()
    {
        const string json = """
            {
              "icaoPrefixes": ["EN", "EF", "EK"],
              "minRunwayFt": 9000,
              "maxRunwayFt": 0,
              "filterCenterIcao": "",
              "filterRadiusNm": 0,
              "excludeVisited": true,
              "startIcao": "",
              "minLegDistanceNm": 0,
              "maxLegDistanceNm": 0,
              "intentSummary": "Northern Europe airports over 9000ft, not yet visited"
            }
            """;

        var result = AnthropicTripPlanningService.ParseQueryFiltersResponse(json);

        Assert.Equal(["EN", "EF", "EK"], result.IcaoPrefixes);
        Assert.Equal(9000, result.MinRunwayFt);
        Assert.True(result.ExcludeVisited);
        Assert.Equal("Northern Europe airports over 9000ft, not yet visited", result.IntentSummary);
    }

    // "Around 200nm -50/+100" should have already been turned into a 150-300 window by Claude
    // (per the prompt instruction) before it ever reaches this parser — this just verifies the
    // parser carries those two fields through correctly.
    [Fact]
    public void ParseQueryFiltersResponse_LegDistanceBounds_AreParsed()
    {
        const string json = """
            {
              "icaoPrefixes": [],
              "minRunwayFt": 0,
              "maxRunwayFt": 0,
              "filterCenterIcao": "",
              "filterRadiusNm": 0,
              "excludeVisited": true,
              "startIcao": "",
              "minLegDistanceNm": 150,
              "maxLegDistanceNm": 300,
              "intentSummary": "legs between 150 and 300nm"
            }
            """;

        var result = AnthropicTripPlanningService.ParseQueryFiltersResponse(json);

        Assert.Equal(150, result.MinLegDistanceNm);
        Assert.Equal(300, result.MaxLegDistanceNm);
    }

    [Fact]
    public void ParseQueryFiltersResponse_MalformedJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => AnthropicTripPlanningService.ParseQueryFiltersResponse("{not valid json"));
    }

    [Fact]
    public void ParseTripPlanResponse_ValidJson_ReturnsOrderedLegs()
    {
        var candidates = new List<Airport> { new() { Icao = "EFHK" }, new() { Icao = "ENGM" }, new() { Icao = "EKCH" } };
        const string json = """
            {
              "title": "Nordic Tour",
              "narrative": "A short scenic tour.",
              "legs": [
                {"order": 2, "departureIcao": "ENGM", "arrivalIcao": "EKCH"},
                {"order": 1, "departureIcao": "EFHK", "arrivalIcao": "ENGM"}
              ]
            }
            """;

        var plan = AnthropicTripPlanningService.ParseTripPlanResponse(json, candidates);

        Assert.Equal("Nordic Tour", plan.Title);
        Assert.Equal(2, plan.Legs.Count);
        Assert.Equal("EFHK", plan.Legs[0].DepartureIcao);
        Assert.Equal("ENGM", plan.Legs[1].DepartureIcao);
        Assert.All(plan.Legs, l => Assert.Equal(TripLegStatus.Planned, l.Status));
    }

    // The model may only sequence airports already in the confirmed candidate list — never
    // introduce a new one. This is the core safety invariant of the whole feature.
    [Fact]
    public void ParseTripPlanResponse_LegReferencesAirportNotInCandidates_DropsThatLeg()
    {
        var candidates = new List<Airport> { new() { Icao = "EFHK" }, new() { Icao = "ENGM" } };
        const string json = """
            {
              "title": "Nordic Tour",
              "narrative": "A short scenic tour.",
              "legs": [
                {"order": 1, "departureIcao": "EFHK", "arrivalIcao": "ENGM"},
                {"order": 2, "departureIcao": "ENGM", "arrivalIcao": "LFPG"}
              ]
            }
            """;

        var plan = AnthropicTripPlanningService.ParseTripPlanResponse(json, candidates);

        Assert.Single(plan.Legs);
        Assert.Equal("EFHK", plan.Legs[0].DepartureIcao);
        Assert.Equal("ENGM", plan.Legs[0].ArrivalIcao);
    }

    [Fact]
    public void ParseTripPlanResponse_MalformedJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => AnthropicTripPlanningService.ParseTripPlanResponse("{not valid json", []));
    }

    [Fact]
    public void BuildNarrateRequest_TargetsSonnet5()
    {
        var legs = new List<TripLeg> { new() { Order = 1, DepartureIcao = "EFHK", ArrivalIcao = "ENGM" } };

        var request = AnthropicTripPlanningService.BuildNarrateRequest(legs, "plan a short-hop trip");

        Assert.Equal("claude-sonnet-5", request.Model.ToString().Trim('"'));
    }

    [Fact]
    public void ParseNarrativeResponse_ValidJson_ReturnsTitleAndNarrative()
    {
        const string json = """{"title": "Nordic Hops", "narrative": "A short-hop tour."}""";

        var result = AnthropicTripPlanningService.ParseNarrativeResponse(json);

        Assert.Equal("Nordic Hops", result.Title);
        Assert.Equal("A short-hop tour.", result.Narrative);
    }

    [Fact]
    public void ParseNarrativeResponse_MalformedJson_Throws()
    {
        Assert.ThrowsAny<Exception>(() => AnthropicTripPlanningService.ParseNarrativeResponse("{not valid json"));
    }
}
