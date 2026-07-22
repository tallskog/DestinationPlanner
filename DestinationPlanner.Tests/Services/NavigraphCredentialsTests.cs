using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Services;

public class NavigraphCredentialsTests
{
    [Fact]
    public void ParseJson_ValidJson_ReturnsCredentials()
    {
        var result = NavigraphCredentials.ParseJson("""{"clientId":"abc","clientSecret":"xyz"}""");

        Assert.NotNull(result);
        Assert.Equal("abc", result!.ClientId);
        Assert.Equal("xyz", result.ClientSecret);
    }

    [Fact]
    public void ParseJson_CaseInsensitivePropertyNames_ReturnsCredentials()
    {
        var result = NavigraphCredentials.ParseJson("""{"CLIENTID":"abc","ClientSecret":"xyz"}""");

        Assert.NotNull(result);
        Assert.Equal("abc", result!.ClientId);
    }

    [Fact]
    public void ParseJson_MissingClientSecret_ReturnsNull()
    {
        Assert.Null(NavigraphCredentials.ParseJson("""{"clientId":"abc"}"""));
    }

    [Fact]
    public void ParseJson_EmptyObject_ReturnsNull()
    {
        Assert.Null(NavigraphCredentials.ParseJson("{}"));
    }

    [Fact]
    public void ParseJson_WhitespaceOnlyValues_ReturnsNull()
    {
        Assert.Null(NavigraphCredentials.ParseJson("""{"clientId":"  ","clientSecret":"xyz"}"""));
    }

    [Fact]
    public void ParseJson_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(NavigraphCredentials.ParseJson("{not valid json"));
    }
}
