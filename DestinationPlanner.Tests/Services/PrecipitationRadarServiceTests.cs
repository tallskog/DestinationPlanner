using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DestinationPlanner.Tests.Services;

public class PrecipitationRadarServiceTests
{
    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    // ---- ParseLatestFrame: JSON -> tile URL template + frame time ----

    [Fact]
    public void ParseLatestFrame_ValidResponse_BuildsTileUrlTemplateFromLastPastFrame()
    {
        const string json = """
            {"host":"https://tilecache.rainviewer.com","radar":{"past":[
                {"time":1784900000,"path":"/v2/radar/old"},
                {"time":1784914800,"path":"/v2/radar/81f6949783f4"}
            ],"nowcast":[]}}
            """;

        var frame = PrecipitationRadarService.ParseLatestFrame(json);

        Assert.NotNull(frame);
        Assert.Equal(
            "https://tilecache.rainviewer.com/v2/radar/81f6949783f4/256/{z}/{x}/{y}/2/1_1.png",
            frame!.TileUrlTemplate);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1784914800), frame.FrameTimeUtc);
    }

    [Fact]
    public void ParseLatestFrame_MissingHost_ReturnsNull()
    {
        const string json = """{"radar":{"past":[{"time":1,"path":"/x"}]}}""";
        Assert.Null(PrecipitationRadarService.ParseLatestFrame(json));
    }

    [Fact]
    public void ParseLatestFrame_MissingRadarProperty_ReturnsNull()
    {
        const string json = """{"host":"https://tilecache.rainviewer.com"}""";
        Assert.Null(PrecipitationRadarService.ParseLatestFrame(json));
    }

    [Fact]
    public void ParseLatestFrame_EmptyPastArray_ReturnsNull()
    {
        const string json = """{"host":"https://tilecache.rainviewer.com","radar":{"past":[]}}""";
        Assert.Null(PrecipitationRadarService.ParseLatestFrame(json));
    }

    [Fact]
    public void ParseLatestFrame_FrameMissingPath_ReturnsNull()
    {
        const string json = """{"host":"https://tilecache.rainviewer.com","radar":{"past":[{"time":1784914800}]}}""";
        Assert.Null(PrecipitationRadarService.ParseLatestFrame(json));
    }

    [Fact]
    public void ParseLatestFrame_FrameMissingTime_ReturnsNull()
    {
        const string json = """{"host":"https://tilecache.rainviewer.com","radar":{"past":[{"path":"/v2/radar/x"}]}}""";
        Assert.Null(PrecipitationRadarService.ParseLatestFrame(json));
    }

    [Fact]
    public void ParseLatestFrame_MalformedJson_ReturnsNullWithoutThrowing()
    {
        Assert.Null(PrecipitationRadarService.ParseLatestFrame("{ not valid json"));
    }

    // ---- GetLatestFrameAsync: HTTP failure handling ----

    [Fact]
    public async Task GetLatestFrameAsync_HttpErrorStatus_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
        };
        var service = new PrecipitationRadarService(new HttpClient(handler));

        var frame = await service.GetLatestFrameAsync(CancellationToken.None);

        Assert.Null(frame);
    }

    [Fact]
    public async Task GetLatestFrameAsync_ValidResponse_ReturnsFrame()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(
                """{"host":"https://tilecache.rainviewer.com","radar":{"past":[{"time":1784914800,"path":"/v2/radar/abc"}]}}""")),
        };
        var service = new PrecipitationRadarService(new HttpClient(handler));

        var frame = await service.GetLatestFrameAsync(CancellationToken.None);

        Assert.NotNull(frame);
        Assert.Contains("/v2/radar/abc/256/{z}/{x}/{y}/", frame!.TileUrlTemplate);
    }
}
