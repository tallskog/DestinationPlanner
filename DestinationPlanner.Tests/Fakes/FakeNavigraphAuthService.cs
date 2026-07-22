using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Fakes;

public class FakeNavigraphAuthService : INavigraphAuthService
{
    public bool IsConfigured { get; set; } = true;

    public NavigraphDeviceAuthorization DeviceAuthorizationToReturn { get; set; } =
        new("device-code", "ABCD-1234", "https://navigraph.com/code", null, ExpiresInSeconds: 1800, IntervalSeconds: 0);

    public Func<NavigraphDeviceAuthorization, CancellationToken, Task<NavigraphTokenResult>>? PollBehavior { get; set; }
    public Func<string, CancellationToken, Task<NavigraphTokenResult>>? RefreshBehavior { get; set; }

    public Task<NavigraphDeviceAuthorization> StartDeviceAuthorizationAsync(CancellationToken ct) =>
        Task.FromResult(DeviceAuthorizationToReturn);

    public Task<NavigraphTokenResult> PollForTokenAsync(NavigraphDeviceAuthorization authorization, CancellationToken ct) =>
        PollBehavior?.Invoke(authorization, ct) ?? throw new InvalidOperationException($"{nameof(PollBehavior)} not configured for this test.");

    public Task<NavigraphTokenResult> RefreshAsync(string refreshToken, CancellationToken ct) =>
        RefreshBehavior?.Invoke(refreshToken, ct) ?? throw new InvalidOperationException($"{nameof(RefreshBehavior)} not configured for this test.");
}
