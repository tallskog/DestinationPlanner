using DestinationPlanner.Helpers;
using System.IO;
using System.Text.Json;

namespace DestinationPlanner.Services;

// OpenAIP API key. Loaded from a local, never-committed file in AppData — see
// requirements.md US34. Never compiled into the binary.
public record OpenAipCredentials(string ApiKey)
{
    private static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions _writeOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string FilePath => Path.Combine(AppDataHelper.AppDataPath, "openaip.local.json");

    public static OpenAipCredentials? TryLoad()
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

    // Writes the API key to openaip.local.json so the user isn't prompted again next sync.
    public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(this, _writeOptions));

    // Extracted so the parsing logic can be unit tested without touching AppData.
    internal static OpenAipCredentials? ParseJson(string json)
    {
        try
        {
            var creds = JsonSerializer.Deserialize<OpenAipCredentials>(json, _readOptions);
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
