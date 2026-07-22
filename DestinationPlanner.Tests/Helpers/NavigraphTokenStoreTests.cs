using DestinationPlanner.Helpers;

namespace DestinationPlanner.Tests.Helpers;

public class NavigraphTokenStoreTests
{
    [Fact]
    public void ProtectThenUnprotect_RoundTripsOriginalValue()
    {
        const string original = "sample-refresh-token-value";

        string protectedValue = NavigraphTokenStore.Protect(original);
        string? recovered = NavigraphTokenStore.Unprotect(protectedValue);

        Assert.Equal(original, recovered);
        Assert.NotEqual(original, protectedValue); // sanity check it's actually encrypted, not passed through
    }

    [Fact]
    public void Unprotect_InvalidBase64_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(NavigraphTokenStore.Unprotect("not valid base64!!!"));
    }

    [Fact]
    public void Unprotect_ValidBase64ButNotDpapiProtected_ReturnsNullInsteadOfThrowing()
    {
        string arbitraryBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("just some bytes"));

        Assert.Null(NavigraphTokenStore.Unprotect(arbitraryBase64));
    }

    [Fact]
    public void TryLoad_NoStoredToken_ReturnsNull()
    {
        var settings = new AppSettings { NavigraphRefreshTokenProtected = null };

        Assert.Null(NavigraphTokenStore.TryLoad(settings));
    }
}
