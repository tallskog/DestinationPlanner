using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DestinationPlanner.Tests.Services;

public class WindDataServiceTests
{
    private static readonly DateTime Now = new(2026, 7, 24, 14, 0, 0, DateTimeKind.Utc);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // ---- ParseGrid: JSON -> WindSample list ----

    [Fact]
    public void ParseGrid_ValidResponse_PicksCurrentHourIndexForEachLocation()
    {
        const string json = """
            [
              {"latitude":51.5,"longitude":-0.1,"hourly":{
                "time":["2026-07-24T13:00","2026-07-24T14:00","2026-07-24T15:00"],
                "wind_speed_500hPa":[10.0,20.0,30.0],
                "wind_direction_500hPa":[100,200,300]
              }},
              {"latitude":60.3,"longitude":24.9,"hourly":{
                "time":["2026-07-24T13:00","2026-07-24T14:00","2026-07-24T15:00"],
                "wind_speed_500hPa":[5.0,15.0,25.0],
                "wind_direction_500hPa":[50,150,250]
              }}
            ]
            """;

        var result = WindDataService.ParseGrid(json, 500, Now);

        Assert.Equal(2, result.Count);
        Assert.Equal(new DestinationPlanner.Models.WindSample(51.5, -0.1, 200, 20.0), result[0]);
        Assert.Equal(new DestinationPlanner.Models.WindSample(60.3, 24.9, 150, 15.0), result[1]);
    }

    [Fact]
    public void ParseGrid_CurrentHourMissing_FallsBackToLastAvailableHour()
    {
        const string json = """
            [{"latitude":51.5,"longitude":-0.1,"hourly":{
                "time":["2026-07-24T00:00","2026-07-24T01:00"],
                "wind_speed_500hPa":[10.0,99.0],
                "wind_direction_500hPa":[100,199]
            }}]
            """;

        var result = WindDataService.ParseGrid(json, 500, Now);

        Assert.Single(result);
        Assert.Equal(99.0, result[0].SpeedKt);
        Assert.Equal(199, result[0].DirectionDeg);
    }

    [Fact]
    public void ParseGrid_LocationMissingHourlyProperty_IsSkippedButOthersStillParsed()
    {
        const string json = """
            [
              {"latitude":1.0,"longitude":2.0},
              {"latitude":51.5,"longitude":-0.1,"hourly":{
                "time":["2026-07-24T14:00"],
                "wind_speed_500hPa":[20.0],
                "wind_direction_500hPa":[200]
              }}
            ]
            """;

        var result = WindDataService.ParseGrid(json, 500, Now);

        Assert.Single(result);
        Assert.Equal(51.5, result[0].Latitude);
    }

    [Fact]
    public void ParseGrid_MissingSpeedArrayForRequestedLevel_LocationIsSkipped()
    {
        const string json = """
            [{"latitude":51.5,"longitude":-0.1,"hourly":{
                "time":["2026-07-24T14:00"],
                "wind_speed_700hPa":[20.0],
                "wind_direction_700hPa":[200]
            }}]
            """;

        var result = WindDataService.ParseGrid(json, 500, Now);

        Assert.Empty(result);
    }

    [Fact]
    public void ParseGrid_MalformedJson_ReturnsEmptyWithoutThrowing()
    {
        var result = WindDataService.ParseGrid("{ not valid", 500, Now);
        Assert.Empty(result);
    }

    // ---- GetWindGridAsync: HTTP behavior ----

    // Zero-length delays: these tests aren't exercising the backoff wait itself, and a real
    // 1s/2s delay per retry test would make the suite noticeably slower for no benefit.
    private static readonly TimeSpan[] NoRetryDelay = [TimeSpan.Zero, TimeSpan.Zero];

    [Fact]
    public async Task GetWindGridAsync_EmptyPoints_ReturnsEmptyWithoutCallingHttp()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => throw new InvalidOperationException("Should not be called for an empty point list."),
        };
        var service = new WindDataService(new HttpClient(handler));

        var result = await service.GetWindGridAsync([], 500, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Empty(result.Samples);
    }

    [Fact]
    public async Task GetWindGridAsync_ServiceUnavailable_RetriesThenReturnsServiceUnavailableFailure()
    {
        int callCount = 0;
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            },
        };
        var service = new WindDataService(new HttpClient(handler), NoRetryDelay);

        var result = await service.GetWindGridAsync([(51.5, -0.1)], 500, CancellationToken.None);

        Assert.Equal(WindFetchFailure.ServiceUnavailable, result.Failure);
        Assert.Empty(result.Samples);
        // One initial attempt plus one retry per configured delay.
        Assert.Equal(NoRetryDelay.Length + 1, callCount);
    }

    [Fact]
    public async Task GetWindGridAsync_TooManyRequests_RetriesThenReturnsRateLimitedFailure()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
        };
        var service = new WindDataService(new HttpClient(handler), NoRetryDelay);

        var result = await service.GetWindGridAsync([(51.5, -0.1)], 500, CancellationToken.None);

        Assert.Equal(WindFetchFailure.RateLimited, result.Failure);
    }

    [Fact]
    public async Task GetWindGridAsync_TransientFailureThenSuccess_RetrySucceeds()
    {
        int callCount = 0;
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) =>
            {
                callCount++;
                if (callCount == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
                return Task.FromResult(JsonResponse(
                    """
                    [{"latitude":51.5,"longitude":-0.1,"hourly":{
                        "time":["2026-07-24T14:00"],
                        "wind_speed_500hPa":[20.0],
                        "wind_direction_500hPa":[200]
                    }}]
                    """));
            },
        };
        var service = new WindDataService(new HttpClient(handler), NoRetryDelay);

        var result = await service.GetWindGridAsync([(51.5, -0.1)], 500, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Single(result.Samples);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetWindGridAsync_NonTransientHttpError_FailsImmediatelyWithoutRetrying()
    {
        int callCount = 0;
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            },
        };
        var service = new WindDataService(new HttpClient(handler), NoRetryDelay);

        var result = await service.GetWindGridAsync([(51.5, -0.1)], 500, CancellationToken.None);

        Assert.Equal(WindFetchFailure.NetworkError, result.Failure);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetWindGridAsync_ValidResponse_ReturnsParsedSamples()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(
                """
                [{"latitude":51.5,"longitude":-0.1,"hourly":{
                    "time":["2026-07-24T14:00"],
                    "wind_speed_500hPa":[20.0],
                    "wind_direction_500hPa":[200]
                }}]
                """)),
        };
        var service = new WindDataService(new HttpClient(handler));

        var result = await service.GetWindGridAsync([(51.5, -0.1)], 500, CancellationToken.None);

        Assert.Null(result.Failure);
        Assert.Single(result.Samples);
        Assert.Equal(20.0, result.Samples[0].SpeedKt);
    }
}
