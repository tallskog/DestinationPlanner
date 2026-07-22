namespace DestinationPlanner.Services;

public record NavigraphDeviceAuthorization(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    int ExpiresInSeconds,
    int IntervalSeconds);

public record NavigraphTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc);

public enum NavigraphAuthErrorKind { AccessDenied, ExpiredToken, Other }

public class NavigraphAuthException : Exception
{
    public NavigraphAuthErrorKind Kind { get; }

    public NavigraphAuthException(NavigraphAuthErrorKind kind, string message) : base(message)
        => Kind = kind;
}

// Thrown when a Navigraph operation is attempted but no client_id/client_secret
// has been configured (see NavigraphCredentials.TryLoad).
public class NavigraphNotConfiguredException : Exception
{
    public NavigraphNotConfiguredException()
        : base("Navigraph integration is not configured on this build.") { }
}
