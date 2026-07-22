using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using DestinationPlanner.ViewModels;

namespace DestinationPlanner.Tests.ViewModels;

public class NavigraphSignInViewModelTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met within the timeout.");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task StartAsync_Success_PopulatesResultAndReachesSuccessState()
    {
        var tokenResult = new NavigraphTokenResult("access-token", "refresh-token", DateTime.UtcNow.AddHours(1));
        var auth = new FakeNavigraphAuthService { PollBehavior = (_, _) => Task.FromResult(tokenResult) };
        var vm = new NavigraphSignInViewModel(auth);

        await vm.StartAsync();

        Assert.Equal(NavigraphSignInState.Success, vm.State);
        Assert.Equal(tokenResult, vm.Result);
        Assert.Equal("ABCD-1234", vm.UserCode);
    }

    [Fact]
    public async Task StartAsync_AccessDenied_ReachesDeniedState()
    {
        var auth = new FakeNavigraphAuthService
        {
            PollBehavior = (_, _) => throw new NavigraphAuthException(NavigraphAuthErrorKind.AccessDenied, "Sign-in was denied."),
        };
        var vm = new NavigraphSignInViewModel(auth);

        await vm.StartAsync();

        Assert.Equal(NavigraphSignInState.Denied, vm.State);
        Assert.Equal("Sign-in was denied.", vm.ErrorMessage);
    }

    [Fact]
    public async Task StartAsync_ExpiredToken_ReachesExpiredState()
    {
        var auth = new FakeNavigraphAuthService
        {
            PollBehavior = (_, _) => throw new NavigraphAuthException(NavigraphAuthErrorKind.ExpiredToken, "The sign-in code expired."),
        };
        var vm = new NavigraphSignInViewModel(auth);

        await vm.StartAsync();

        Assert.Equal(NavigraphSignInState.Expired, vm.State);
    }

    [Fact]
    public async Task StartAsync_UnexpectedException_ReachesErrorState()
    {
        var auth = new FakeNavigraphAuthService
        {
            PollBehavior = (_, _) => throw new InvalidOperationException("boom"),
        };
        var vm = new NavigraphSignInViewModel(auth);

        await vm.StartAsync();

        Assert.Equal(NavigraphSignInState.Error, vm.State);
        Assert.Equal("boom", vm.ErrorMessage);
    }

    [Fact]
    public async Task CancelCommand_WhileWaitingForUser_ReachesCancelledState()
    {
        var auth = new FakeNavigraphAuthService
        {
            PollBehavior = async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable — Task.Delay should have thrown on cancellation");
            },
        };
        var vm = new NavigraphSignInViewModel(auth);

        var startTask = vm.StartAsync();
        await WaitUntilAsync(() => vm.State == NavigraphSignInState.WaitingForUser);
        vm.CancelCommand.Execute(null);
        await startTask;

        Assert.Equal(NavigraphSignInState.Cancelled, vm.State);
    }

    [Fact]
    public async Task Completed_EventFiresExactlyOnceOnSuccess()
    {
        var tokenResult = new NavigraphTokenResult("access-token", "refresh-token", DateTime.UtcNow.AddHours(1));
        var auth = new FakeNavigraphAuthService { PollBehavior = (_, _) => Task.FromResult(tokenResult) };
        var vm = new NavigraphSignInViewModel(auth);
        int completedCount = 0;
        vm.Completed += (_, _) => completedCount++;

        await vm.StartAsync();

        Assert.Equal(1, completedCount);
    }
}
