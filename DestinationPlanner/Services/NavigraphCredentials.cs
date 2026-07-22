using DestinationPlanner.Helpers;
using System.IO;
using System.Text.Json;

namespace DestinationPlanner.Services;

// Navigraph developer client_id/client_secret. Loaded from a local, never-committed
// file in AppData — see requirements.md US34. Never compiled into the binary.
public record NavigraphCredentials(string ClientId, string ClientSecret)
{
    private static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };

    public static NavigraphCredentials? TryLoad()
    {
        string path = Path.Combine(AppDataHelper.AppDataPath, "navigraph.local.json");
        if (!File.Exists(path)) return null;

        try
        {
            return ParseJson(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    // Extracted so the parsing logic can be unit tested without touching AppData.
    internal static NavigraphCredentials? ParseJson(string json)
    {
        try
        {
            var creds = JsonSerializer.Deserialize<NavigraphCredentials>(json, _readOptions);
            if (creds is null || string.IsNullOrWhiteSpace(creds.ClientId) || string.IsNullOrWhiteSpace(creds.ClientSecret))
                return null;
            return creds;
        }
        catch
        {
            return null;
        }
    }
}
