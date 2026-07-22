namespace DestinationPlanner.Services;

public interface INavigraphAuthService
{
    bool IsConfigured { get; }

    // Starts the OAuth 2.0 Device Authorization Flow with PKCE, generating and
    // caching a code_verifier internally for the subsequent PollForTokenAsync call.
    Task<NavigraphDeviceAuthorization> StartDeviceAuthorizationAsync(CancellationToken ct);

    // Polls the token endpoint until the user approves, denies, or the code expires.
    Task<NavigraphTokenResult> PollForTokenAsync(NavigraphDeviceAuthorization authorization, CancellationToken ct);

    // Refresh tokens are single-use — the NavigraphTokenResult.RefreshToken returned
    // here must be persisted immediately; the token passed in is no longer valid afterward.
    Task<NavigraphTokenResult> RefreshAsync(string refreshToken, CancellationToken ct);
}
