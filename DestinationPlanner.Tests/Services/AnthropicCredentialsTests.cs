using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Services;

public class AnthropicCredentialsTests
{
    [Fact]
    public void ParseJson_ValidJson_ReturnsCredentials()
    {
        var result = AnthropicCredentials.ParseJson("""{"apiKey":"abc123"}""");

        Assert.NotNull(result);
        Assert.Equal("abc123", result!.ApiKey);
    }

    [Fact]
    public void ParseJson_CaseInsensitivePropertyNames_ReturnsCredentials()
    {
        var result = AnthropicCredentials.ParseJson("""{"APIKEY":"abc123"}""");

        Assert.NotNull(result);
        Assert.Equal("abc123", result!.ApiKey);
    }

    [Fact]
    public void ParseJson_MissingApiKey_ReturnsNull()
    {
        Assert.Null(AnthropicCredentials.ParseJson("{}"));
    }

    [Fact]
    public void ParseJson_WhitespaceOnlyApiKey_ReturnsNull()
    {
        Assert.Null(AnthropicCredentials.ParseJson("""{"apiKey":"   "}"""));
    }

    [Fact]
    public void ParseJson_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        Assert.Null(AnthropicCredentials.ParseJson("{not valid json"));
    }
}
