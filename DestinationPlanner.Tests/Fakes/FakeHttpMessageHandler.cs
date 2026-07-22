using System.Net.Http;

namespace DestinationPlanner.Tests.Fakes;

// Lets tests script canned HTTP responses for NavigraphAuthService without any real network I/O.
public class FakeHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } =
        (_, _) => throw new InvalidOperationException($"{nameof(Handler)} not configured for this test.");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Handler(request, cancellationToken);
}
