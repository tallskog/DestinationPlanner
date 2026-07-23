using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Services;

public class OpenAipCredentialsTests
{
    [Fact]
    public void ParseJson_ValidJson_ReturnsCredentials()
    {
        var result = OpenAipCredentials.ParseJson("""{"apiKey":"abc123"}""");

        Assert.NotNull(result);
        Assert.Equal("abc123", result!.ApiKey);
    }

    [Fact]
    public void ParseJson_CaseInsensitivePropertyNames_ReturnsCredentials()
    {
        var result = OpenAipCredentials.ParseJson("""{"APIKEY":"abc123"}""");

        Assert.NotNull(result);
        Assert.Equal("abc123", result!.ApiKey);
    }

    [Fact]
    public void ParseJson_MissingApiKey_ReturnsNull()
    {
        Assert.Null(OpenAipCredentials.ParseJson("{}"));
    }

    [Fact]
    public void ParseJson_EmptyObject_ReturnsNull()
    {
        Assert.Null(OpenAipCredentials.ParseJson("{}"));
    }

    [Fact]
    public void ParseJson_WhitespaceOnlyApiKey_ReturnsNull()
    {
        Assert.Null(OpenAipCredentials.ParseJson("""{"apiKey":"   "}"""));
    }

    [Fact]
    public void ParseJson_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(OpenAipCredentials.ParseJson("{not valid json"));
    }
}
