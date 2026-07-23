using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DestinationPlanner.Tests.Services;

public class OpenAipDataServiceTests
{
    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static int GetPageQueryParam(HttpRequestMessage request)
    {
        string query = request.RequestUri!.Query.TrimStart('?');
        var pair = query.Split('&')
            .Select(p => p.Split('=', 2))
            .First(p => p[0] == "page");
        return int.Parse(pair[1]);
    }

    // ---- MapType: the OpenAIP type/private -> AirportType mapping table ----

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    public void MapType_HeliportCodes_MapToHeliport(int type) =>
        Assert.Equal(AirportType.Heliport, OpenAipDataService.MapType(type, isPrivate: false));

    [Fact]
    public void MapType_MilitaryAerodrome_MapsToMilitary() =>
        Assert.Equal(AirportType.Military, OpenAipDataService.MapType(5, isPrivate: false));

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    public void MapType_CivilCodes_MapToCivil(int type) =>
        Assert.Equal(AirportType.Civil, OpenAipDataService.MapType(type, isPrivate: false));

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void MapType_NicheCodes_MapToOther(int type) =>
        Assert.Equal(AirportType.Other, OpenAipDataService.MapType(type, isPrivate: false));

    [Fact]
    public void MapType_NullType_MapsToUnknown_DistinctFromOther()
    {
        Assert.Equal(AirportType.Unknown, OpenAipDataService.MapType(null, isPrivate: false));
    }

    [Fact]
    public void MapType_UnrecognizedFutureCode_MapsToOther_NeverThrows()
    {
        Assert.Equal(AirportType.Other, OpenAipDataService.MapType(99, isPrivate: false));
    }

    [Theory]
    [InlineData(4)]  // Heliport
    [InlineData(5)]  // Military
    [InlineData(3)]  // Civil
    [InlineData(1)]  // Other
    [InlineData(null)] // Unknown
    public void MapType_PrivateFlagWinsRegardlessOfType(int? type)
    {
        Assert.Equal(AirportType.Private, OpenAipDataService.MapType(type, isPrivate: true));
    }

    // ---- FetchAirportTypesAsync: HTTP + pagination behavior ----

    [Fact]
    public async Task FetchAirportTypesAsync_SendsApiKeyHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler
        {
            Handler = (req, _) =>
            {
                capturedRequest = req;
                return Task.FromResult(JsonResponse(
                    """{"page":1,"limit":1000,"totalCount":0,"totalPages":1,"items":[]}"""));
            },
        };
        var service = new OpenAipDataService(new HttpClient(handler));

        await service.FetchAirportTypesAsync("test-key", CancellationToken.None);

        Assert.True(capturedRequest!.Headers.TryGetValues("x-openaip-api-key", out var values));
        Assert.Equal("test-key", values!.Single());
    }

    [Fact]
    public async Task FetchAirportTypesAsync_ParsesIcaoTypeAndPrivate()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(
                """
                {"page":1,"limit":1000,"totalCount":3,"totalPages":1,"items":[
                    {"icaoCode":"EFHK","type":3,"private":false},
                    {"icaoCode":"EFTU","type":5,"private":false},
                    {"icaoCode":"EGLL","type":2,"private":true}
                ]}
                """)),
        };
        var service = new OpenAipDataService(new HttpClient(handler));

        var result = await service.FetchAirportTypesAsync("test-key", CancellationToken.None);

        Assert.Equal(AirportType.Civil, result["EFHK"]);
        Assert.Equal(AirportType.Military, result["EFTU"]);
        Assert.Equal(AirportType.Private, result["EGLL"]);
    }

    [Fact]
    public async Task FetchAirportTypesAsync_ItemsWithoutIcaoCode_AreSkipped()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(
                """
                {"page":1,"limit":1000,"totalCount":2,"totalPages":1,"items":[
                    {"type":3,"private":false},
                    {"icaoCode":"EFHK","type":3,"private":false}
                ]}
                """)),
        };
        var service = new OpenAipDataService(new HttpClient(handler));

        var result = await service.FetchAirportTypesAsync("test-key", CancellationToken.None);

        Assert.Single(result);
        Assert.True(result.ContainsKey("EFHK"));
    }

    [Fact]
    public async Task FetchAirportTypesAsync_IcaoKeyLookupIsCaseInsensitive()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(
                """{"page":1,"limit":1000,"totalCount":1,"totalPages":1,"items":[{"icaoCode":"EFHK","type":3,"private":false}]}""")),
        };
        var service = new OpenAipDataService(new HttpClient(handler));

        var result = await service.FetchAirportTypesAsync("test-key", CancellationToken.None);

        Assert.True(result.ContainsKey("efhk"));
    }

    [Fact]
    public async Task FetchAirportTypesAsync_StopsPaginatingAtTotalPages()
    {
        var requestedPages = new List<int>();
        var handler = new FakeHttpMessageHandler
        {
            Handler = (req, _) =>
            {
                int page = GetPageQueryParam(req);
                requestedPages.Add(page);

                string icao = $"AA{page:D2}";
                return Task.FromResult(JsonResponse(
                    $$"""{"page":{{page}},"limit":1000,"totalCount":3,"totalPages":3,"items":[{"icaoCode":"{{icao}}","type":3,"private":false}]}"""));
            },
        };
        var service = new OpenAipDataService(new HttpClient(handler));

        var result = await service.FetchAirportTypesAsync("test-key", CancellationToken.None);

        Assert.Equal([1, 2, 3], requestedPages);
        Assert.Equal(3, result.Count);
    }
}
