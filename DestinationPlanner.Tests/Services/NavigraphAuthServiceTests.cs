using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace DestinationPlanner.Tests.Services;

public class NavigraphAuthServiceTests
{
    private const string DeviceAuthResponse =
        """{"device_code":"dc123","user_code":"ABCD-1234","verification_uri":"https://navigraph.com/code","verification_uri_complete":"https://navigraph.com/code?u=ABCD-1234","expires_in":1800,"interval":0}""";

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpRequestMessage request)
    {
        string body = await request.Content!.ReadAsStringAsync();
        return body.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
    }

    // Returns a handler that answers the device-authorization request normally, then
    // answers every token-endpoint request with the given status/body.
    private static FakeHttpMessageHandler DeviceAuthThenTokenHandler(HttpStatusCode tokenStatus, string tokenBody)
    {
        var handler = new FakeHttpMessageHandler();
        handler.Handler = (req, _) => Task.FromResult(
            req.RequestUri!.AbsoluteUri.Contains("deviceauthorization")
                ? JsonResponse(HttpStatusCode.OK, DeviceAuthResponse)
                : JsonResponse(tokenStatus, tokenBody));
        return handler;
    }

    [Fact]
    public async Task StartDeviceAuthorizationAsync_SendsExpectedFieldsAndParsesResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler
        {
            Handler = async (req, _) =>
            {
                capturedRequest = req;
                return JsonResponse(HttpStatusCode.OK, DeviceAuthResponse);
            },
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("client-id", "client-secret"), new HttpClient(handler));

        var result = await service.StartDeviceAuthorizationAsync(CancellationToken.None);

        Assert.Equal("dc123", result.DeviceCode);
        Assert.Equal("ABCD-1234", result.UserCode);
        Assert.Equal("https://navigraph.com/code", result.VerificationUri);
        Assert.Equal("https://navigraph.com/code?u=ABCD-1234", result.VerificationUriComplete);
        Assert.Equal(1800, result.ExpiresInSeconds);

        Assert.Equal("https://identity.api.navigraph.com/connect/deviceauthorization", capturedRequest!.RequestUri!.ToString());
        var form = await ReadFormAsync(capturedRequest);
        Assert.Equal("client-id", form["client_id"]);
        Assert.Equal("client-secret", form["client_secret"]);
        Assert.Equal("S256", form["code_challenge_method"]);
        Assert.Equal("openid offline_access fmsdata", form["scope"]);
        Assert.False(string.IsNullOrEmpty(form["code_challenge"]));
    }

    [Fact]
    public async Task StartDeviceAuthorizationAsync_NoCredentials_ThrowsNotConfigured()
    {
        var service = new NavigraphAuthService(null, new HttpClient(new FakeHttpMessageHandler()));

        await Assert.ThrowsAsync<NavigraphNotConfiguredException>(
            () => service.StartDeviceAuthorizationAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PollForTokenAsync_CodeVerifierMatchesEarlierCodeChallenge_PkceRoundTripIsCorrect()
    {
        string? capturedChallenge = null;
        string? capturedVerifier = null;
        var handler = new FakeHttpMessageHandler();
        handler.Handler = async (req, _) =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("deviceauthorization"))
            {
                capturedChallenge = (await ReadFormAsync(req))["code_challenge"];
                return JsonResponse(HttpStatusCode.OK, DeviceAuthResponse);
            }

            var tokenForm = await ReadFormAsync(req);
            capturedVerifier = tokenForm["code_verifier"];
            Assert.Equal("urn:ietf:params:oauth:grant-type:device_code", tokenForm["grant_type"]);
            Assert.Equal("dc123", tokenForm["device_code"]);
            return JsonResponse(HttpStatusCode.OK, """{"access_token":"at1","refresh_token":"rt1","expires_in":3600}""");
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));

        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);
        var token = await service.PollForTokenAsync(auth, CancellationToken.None);

        Assert.Equal("at1", token.AccessToken);
        Assert.Equal("rt1", token.RefreshToken);

        // Verify actual PKCE correctness: SHA256(verifier), base64url-encoded, must equal
        // the code_challenge sent in the initial device authorization request.
        string recomputedChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(capturedVerifier!)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Equal(capturedChallenge, recomputedChallenge);
    }

    [Fact]
    public async Task PollForTokenAsync_AuthorizationPendingThenSuccess_RetriesUntilTokenIssued()
    {
        int tokenCallCount = 0;
        var handler = new FakeHttpMessageHandler();
        handler.Handler = (req, _) =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("deviceauthorization"))
                return Task.FromResult(JsonResponse(HttpStatusCode.OK, DeviceAuthResponse));

            tokenCallCount++;
            return Task.FromResult(tokenCallCount < 2
                ? JsonResponse(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}""")
                : JsonResponse(HttpStatusCode.OK, """{"access_token":"at1","refresh_token":"rt1","expires_in":3600}"""));
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));

        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);
        var token = await service.PollForTokenAsync(auth, CancellationToken.None);

        Assert.Equal(2, tokenCallCount);
        Assert.Equal("at1", token.AccessToken);
    }

    [Fact]
    public async Task PollForTokenAsync_AccessDenied_ThrowsWithAccessDeniedKind()
    {
        var handler = DeviceAuthThenTokenHandler(HttpStatusCode.BadRequest, """{"error":"access_denied"}""");
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));
        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<NavigraphAuthException>(() => service.PollForTokenAsync(auth, CancellationToken.None));

        Assert.Equal(NavigraphAuthErrorKind.AccessDenied, ex.Kind);
    }

    [Fact]
    public async Task PollForTokenAsync_ServerReportedExpiredToken_ThrowsWithExpiredTokenKind()
    {
        var handler = DeviceAuthThenTokenHandler(HttpStatusCode.BadRequest, """{"error":"expired_token"}""");
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));
        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<NavigraphAuthException>(() => service.PollForTokenAsync(auth, CancellationToken.None));

        Assert.Equal(NavigraphAuthErrorKind.ExpiredToken, ex.Kind);
    }

    [Fact]
    public async Task PollForTokenAsync_UnrecognizedErrorCode_ThrowsWithOtherKind()
    {
        var handler = DeviceAuthThenTokenHandler(HttpStatusCode.BadRequest, """{"error":"server_error"}""");
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));
        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<NavigraphAuthException>(() => service.PollForTokenAsync(auth, CancellationToken.None));

        Assert.Equal(NavigraphAuthErrorKind.Other, ex.Kind);
    }

    [Fact]
    public async Task PollForTokenAsync_DeviceCodeExpiresLocally_ThrowsExpiredTokenWithoutCallingServer()
    {
        bool tokenEndpointCalled = false;
        var handler = new FakeHttpMessageHandler();
        handler.Handler = (req, _) =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("deviceauthorization"))
                return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                    """{"device_code":"dc123","user_code":"ABCD-1234","verification_uri":"https://navigraph.com/code","expires_in":0,"interval":0}"""));

            tokenEndpointCalled = true;
            return Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}"""));
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));
        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<NavigraphAuthException>(() => service.PollForTokenAsync(auth, CancellationToken.None));

        Assert.Equal(NavigraphAuthErrorKind.ExpiredToken, ex.Kind);
        Assert.False(tokenEndpointCalled);
    }

    [Fact]
    public async Task PollForTokenAsync_WithoutPriorStart_ThrowsInvalidOperationException()
    {
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(new FakeHttpMessageHandler()));
        var fakeAuth = new NavigraphDeviceAuthorization("dc", "uc", "uri", null, 1800, 5);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PollForTokenAsync(fakeAuth, CancellationToken.None));
    }

    [Fact]
    public async Task PollForTokenAsync_AlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, DeviceAuthResponse)),
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));
        var auth = await service.StartDeviceAuthorizationAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PollForTokenAsync(auth, cts.Token));
    }

    [Fact]
    public async Task RefreshAsync_Success_ReturnsNewTokenAndSendsRefreshTokenGrant()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler
        {
            Handler = async (req, _) =>
            {
                captured = req;
                return JsonResponse(HttpStatusCode.OK, """{"access_token":"at2","refresh_token":"rt2","expires_in":3600}""");
            },
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));

        var result = await service.RefreshAsync("old-refresh-token", CancellationToken.None);

        Assert.Equal("at2", result.AccessToken);
        Assert.Equal("rt2", result.RefreshToken);
        var form = await ReadFormAsync(captured!);
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("old-refresh-token", form["refresh_token"]);
    }

    [Fact]
    public async Task RefreshAsync_NoCredentials_ThrowsNotConfigured()
    {
        var service = new NavigraphAuthService(null, new HttpClient(new FakeHttpMessageHandler()));

        await Assert.ThrowsAsync<NavigraphNotConfiguredException>(() => service.RefreshAsync("token", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_ServerError_ThrowsNavigraphAuthException()
    {
        var handler = new FakeHttpMessageHandler
        {
            Handler = (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")),
        };
        var service = new NavigraphAuthService(new NavigraphCredentials("id", "secret"), new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<NavigraphAuthException>(() => service.RefreshAsync("dead-token", CancellationToken.None));

        Assert.Equal(NavigraphAuthErrorKind.Other, ex.Kind);
    }
}
