using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using DestinationPlanner.Helpers;
using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// Calls the Claude API (official Anthropic C# SDK) for AI-assisted trip planning (US41).
// All operations use structured output (OutputConfig.Format) so responses are guaranteed
// valid JSON matching the schema below — no free-form prose parsing.
public sealed class AnthropicTripPlanningService : IAiTripPlanningService
{
    private const string Model = "claude-sonnet-5";
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AnthropicClient _client;

    public AnthropicTripPlanningService(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<TripQueryFilters> ParseQueryAsync(string userQuery, CancellationToken ct = default)
    {
        var response = await _client.Messages.Create(BuildParseQueryRequest(userQuery), ct);
        return ParseQueryFiltersResponse(ExtractText(response));
    }

    public async Task<TripPlan> PlanTripAsync(IReadOnlyList<Airport> candidates, string userQuery, string? startIcao, CancellationToken ct = default)
    {
        var response = await _client.Messages.Create(BuildPlanTripRequest(candidates, userQuery, startIcao), ct);
        return ParseTripPlanResponse(ExtractText(response), candidates);
    }

    public async Task<TripNarrative> NarrateAsync(IReadOnlyList<TripLeg> legs, string userQuery, CancellationToken ct = default)
    {
        var response = await _client.Messages.Create(BuildNarrateRequest(legs, userQuery), ct);
        return ParseNarrativeResponse(ExtractText(response));
    }

    private static string ExtractText(Message response)
    {
        if (response.StopReason?.ToString().Trim('"') == "max_tokens")
            throw new InvalidOperationException(
                "Claude's response was cut off before it finished (too many candidate airports for one response). Try narrowing your query.");

        return response.Content.Select(b => b.Value).OfType<TextBlock>().FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("Claude response contained no text block.");
    }

    // Internal (not private) so the request/response helpers below are directly unit-testable
    // without a network call — see AnthropicTripPlanningServiceTests.

    internal static MessageCreateParams BuildParseQueryRequest(string userQuery)
    {
        string regionTableJson = JsonSerializer.Serialize(RegionLookup.Regions);
        string prompt = $"""
            You turn a pilot's free-text trip request into structured airport search filters.

            Known aviation regions and their ICAO prefixes — use these when the query names a
            region; never invent ICAO prefixes from memory:
            {regionTableJson}

            If the query gives a per-leg distance preference (e.g. "around 200nm -50/+100" or
            "legs between 150 and 300nm"), compute minLegDistanceNm/maxLegDistanceNm from it
            (e.g. "around 200nm -50/+100" means minLegDistanceNm=150, maxLegDistanceNm=300). If
            only an approximate figure is given with no explicit tolerance, use a +/-25% window.

            User request: {userQuery}
            """;

        return new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 1024,
            // Disabled: this is a pure structured-output task with no need for exploratory
            // reasoning. Sonnet 5 runs adaptive thinking by default when Thinking is omitted,
            // which shares the same MaxTokens budget as the response — on a small MaxTokens
            // request that silently truncates the JSON output before it's ever written.
            Thinking = new ThinkingConfigDisabled(),
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = QueryFiltersSchema } },
            Messages = [new() { Role = Role.User, Content = prompt }],
        };
    }

    internal static MessageCreateParams BuildPlanTripRequest(IReadOnlyList<Airport> candidates, string userQuery, string? startIcao)
    {
        string candidateList = string.Join(", ", candidates.Select(a => a.Icao));
        string startInstruction = string.IsNullOrWhiteSpace(startIcao) ? "" : $"Start the trip at {startIcao}.\n";
        string prompt = $"""
            Sequence the following airports into an efficient multi-leg trip and write a short
            narrative. You may only use airports from this exact list — never introduce a new one.
            Include every airport in the list exactly once, choosing a sensible visiting order:
            {candidateList}

            {startInstruction}User's original request: {userQuery}
            """;

        return new MessageCreateParams
        {
            Model = Model,
            // Scales with candidate count so a longer list of legs doesn't get silently
            // truncated mid-JSON; floor covers the fixed title/narrative overhead, ceiling
            // keeps the request comfortably under the ~16K non-streaming timeout guidance.
            MaxTokens = Math.Clamp(candidates.Count * 150 + 1024, 2048, 16000),
            Thinking = new ThinkingConfigDisabled(),
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = TripPlanSchema } },
            Messages = [new() { Role = Role.User, Content = prompt }],
        };
    }

    // Used instead of BuildPlanTripRequest when a per-leg distance constraint is present — the
    // leg sequence is already fixed by TripRouteBuilder (computed from real airport coordinates,
    // not the model's memory), so this only asks for a title + narrative around it.
    internal static MessageCreateParams BuildNarrateRequest(IReadOnlyList<TripLeg> legs, string userQuery)
    {
        string legList = string.Join("\n", legs.Select(l => $"{l.Order}. {l.DepartureIcao} -> {l.ArrivalIcao}"));
        string prompt = $"""
            Write a short title and narrative for this already-planned multi-leg trip. Do not
            change, reorder, add, or remove legs — the route is fixed:
            {legList}

            User's original request: {userQuery}
            """;

        return new MessageCreateParams
        {
            Model = Model,
            MaxTokens = 1024,
            Thinking = new ThinkingConfigDisabled(),
            OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = NarrativeSchema } },
            Messages = [new() { Role = Role.User, Content = prompt }],
        };
    }

    internal static TripQueryFilters ParseQueryFiltersResponse(string json) =>
        JsonSerializer.Deserialize<TripQueryFilters>(json, ReadOptions)
        ?? throw new InvalidOperationException("Could not parse trip query filters from Claude's response.");

    internal static TripPlan ParseTripPlanResponse(string json, IReadOnlyList<Airport> candidates)
    {
        var parsed = JsonSerializer.Deserialize<TripPlanResponseDto>(json, ReadOptions)
            ?? throw new InvalidOperationException("Could not parse a trip plan from Claude's response.");

        var validIcaos = candidates.Select(a => a.Icao).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new TripPlan
        {
            Title = parsed.Title,
            Narrative = parsed.Narrative,
            // Only ever keep legs whose airports are in the confirmed candidate list — the model
            // may only reorder/sequence candidates, never introduce a new airport.
            Legs = parsed.Legs
                .Where(l => validIcaos.Contains(l.DepartureIcao) && validIcaos.Contains(l.ArrivalIcao))
                .OrderBy(l => l.Order)
                .Select(l => new TripLeg { Order = l.Order, DepartureIcao = l.DepartureIcao, ArrivalIcao = l.ArrivalIcao })
                .ToList(),
        };
    }

    internal static TripNarrative ParseNarrativeResponse(string json) =>
        JsonSerializer.Deserialize<TripNarrative>(json, ReadOptions)
        ?? throw new InvalidOperationException("Could not parse a trip narrative from Claude's response.");

    private sealed record TripPlanResponseDto(string Title, string Narrative, List<TripLegResponseDto> Legs);
    private sealed record TripLegResponseDto(int Order, string DepartureIcao, string ArrivalIcao);

    private static readonly Dictionary<string, JsonElement> QueryFiltersSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            icaoPrefixes = new { type = "array", items = new { type = "string" }, description = "ICAO prefixes chosen from the provided region table or matching the user's stated countries. Empty array if no region/country restriction is implied." },
            minRunwayFt = new { type = "integer", description = "Minimum longest-runway length in feet. 0 if not specified." },
            maxRunwayFt = new { type = "integer", description = "Maximum longest-runway length in feet. 0 if not specified." },
            filterCenterIcao = new { type = "string", description = "An ICAO code to search near, for 'near X' style phrasing only. Empty string otherwise." },
            filterRadiusNm = new { type = "number", description = "Radius in nautical miles around filterCenterIcao. 0 if not applicable." },
            excludeVisited = new { type = "boolean", description = "True unless the user explicitly wants already-visited airports included." },
            startIcao = new { type = "string", description = "A stated starting airport ICAO code, if any. Empty string otherwise." },
            minLegDistanceNm = new { type = "number", description = "Minimum allowed distance in nautical miles between consecutive airports in the route. 0 if not specified." },
            maxLegDistanceNm = new { type = "number", description = "Maximum allowed distance in nautical miles between consecutive airports in the route. 0 if not specified." },
            intentSummary = new { type = "string", description = "A one-sentence restatement of the interpreted query, for the user to confirm." },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "icaoPrefixes", "minRunwayFt", "maxRunwayFt", "filterCenterIcao", "filterRadiusNm", "excludeVisited", "startIcao", "minLegDistanceNm", "maxLegDistanceNm", "intentSummary" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private static readonly Dictionary<string, JsonElement> TripPlanSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            title = new { type = "string" },
            narrative = new { type = "string" },
            legs = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        order = new { type = "integer" },
                        departureIcao = new { type = "string" },
                        arrivalIcao = new { type = "string" },
                    },
                    required = new[] { "order", "departureIcao", "arrivalIcao" },
                    additionalProperties = false,
                },
            },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "title", "narrative", "legs" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private static readonly Dictionary<string, JsonElement> NarrativeSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            title = new { type = "string" },
            narrative = new { type = "string" },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "title", "narrative" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };
}
