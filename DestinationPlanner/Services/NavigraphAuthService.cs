using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Hand-rolled OAuth 2.0 Device Authorization Flow with PKCE — Navigraph does not
// ship an official .NET SDK. See https://developers.navigraph.com/docs/authentication/device-authorization
public class NavigraphAuthService : INavigraphAuthService
{
    private const string DeviceAuthorizationEndpoint = "https://identity.api.navigraph.com/connect/deviceauthorization";
    private const string TokenEndpoint = "https://identity.api.navigraph.com/connect/token";
    private const string Scope = "openid offline_access fmsdata";

    private readonly HttpClient _http;
    private readonly NavigraphCredentials? _credentials;
    private string? _codeVerifier;

    // httpClient is an injection seam for tests (a fake HttpMessageHandler) — in
    // production a single instance is created once here and reused for the app's lifetime.
    public NavigraphAuthService(NavigraphCredentials? credentials, HttpClient? httpClient = null)
    {
        _credentials = credentials;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public bool IsConfigured => _credentials is not null;

    public async Task<NavigraphDeviceAuthorization> StartDeviceAuthorizationAsync(CancellationToken ct)
    {
        var credentials = RequireCredentials();
        var (verifier, challenge) = GeneratePkce();
        _codeVerifier = verifier;

        var form = new Dictionary<string, string>
        {
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["scope"] = Scope,
        };

        using var response = await _http.PostAsync(DeviceAuthorizationEndpoint, new FormUrlEncodedContent(form), ct);
        response.EnsureSuccessStatusCode();
        var json = await ReadJsonAsync(response, ct);

        return new NavigraphDeviceAuthorization(
            DeviceCode: json.GetProperty("device_code").GetString()!,
            UserCode: json.GetProperty("user_code").GetString()!,
            VerificationUri: json.GetProperty("verification_uri").GetString()!,
            VerificationUriComplete: json.TryGetProperty("verification_uri_complete", out var vuc) ? vuc.GetString() : null,
            ExpiresInSeconds: json.GetProperty("expires_in").GetInt32(),
            IntervalSeconds: json.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5);
    }

    public async Task<NavigraphTokenResult> PollForTokenAsync(NavigraphDeviceAuthorization authorization, CancellationToken ct)
    {
        var credentials = RequireCredentials();
        if (_codeVerifier is null)
            throw new InvalidOperationException($"{nameof(StartDeviceAuthorizationAsync)} must be called first.");

        int intervalSeconds = Math.Max(authorization.IntervalSeconds, 1);
        var deadlineUtc = DateTime.UtcNow.AddSeconds(authorization.ExpiresInSeconds);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadlineUtc)
                throw new NavigraphAuthException(NavigraphAuthErrorKind.ExpiredToken, "The sign-in code expired before it was approved.");

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = authorization.DeviceCode,
                ["code_verifier"] = _codeVerifier,
                ["client_id"] = credentials.ClientId,
                ["client_secret"] = credentials.ClientSecret,
            };

            using var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
            var json = await ReadJsonAsync(response, ct);

            if (response.IsSuccessStatusCode)
                return ParseTokenResult(json);

            string error = json.TryGetProperty("error", out var e) ? e.GetString() ?? "" : "";
            switch (error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    intervalSeconds += 5;
                    continue;
                case "access_denied":
                    throw new NavigraphAuthException(NavigraphAuthErrorKind.AccessDenied, "Sign-in was denied.");
                case "expired_token":
                    throw new NavigraphAuthException(NavigraphAuthErrorKind.ExpiredToken, "The sign-in code expired before it was approved.");
                default:
                    throw new NavigraphAuthException(NavigraphAuthErrorKind.Other,
                        string.IsNullOrEmpty(error) ? "Navigraph sign-in failed." : $"Navigraph sign-in failed: {error}");
            }
        }
    }

    public async Task<NavigraphTokenResult> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var credentials = RequireCredentials();

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = credentials.ClientId,
            ["client_secret"] = credentials.ClientSecret,
            ["refresh_token"] = refreshToken,
        };

        using var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        var json = await ReadJsonAsync(response, ct);

        if (!response.IsSuccessStatusCode)
            throw new NavigraphAuthException(NavigraphAuthErrorKind.Other, "Failed to refresh the Navigraph session — sign-in required again.");

        return ParseTokenResult(json);
    }

    private NavigraphCredentials RequireCredentials() => _credentials ?? throw new NavigraphNotConfiguredException();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        return doc.RootElement.Clone();
    }

    private static NavigraphTokenResult ParseTokenResult(JsonElement json) => new(
        AccessToken: json.GetProperty("access_token").GetString()!,
        RefreshToken: json.GetProperty("refresh_token").GetString()!,
        AccessTokenExpiresAtUtc: DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()));

    private static (string verifier, string challenge) GeneratePkce()
    {
        string verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
