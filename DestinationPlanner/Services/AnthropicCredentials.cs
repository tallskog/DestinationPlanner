using DestinationPlanner.Helpers;
using System.IO;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Anthropic (Claude) API key for AI-assisted trip planning (US41). Loaded from a local,
// never-committed file in AppData. Never compiled into the binary. Mirrors
// OpenAipCredentials.cs exactly.
public record AnthropicCredentials(string ApiKey)
{
    private static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions _writeOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string FilePath => Path.Combine(AppDataHelper.AppDataPath, "anthropic.local.json");

    public static AnthropicCredentials? TryLoad()
    {
        if (!File.Exists(FilePath)) return null;

        try
        {
            return ParseJson(File.ReadAllText(FilePath));
        }
        catch
        {
            return null;
        }
    }

    // Writes the API key to anthropic.local.json so the user isn't prompted again next time.
    public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(this, _writeOptions));

    // Extracted so the parsing logic can be unit tested without touching AppData.
    internal static AnthropicCredentials? ParseJson(string json)
    {
        try
        {
            var creds = JsonSerializer.Deserialize<AnthropicCredentials>(json, _readOptions);
            if (creds is null || string.IsNullOrWhiteSpace(creds.ApiKey))
                return null;
            return creds;
        }
        catch
        {
            return null;
        }
    }
}
