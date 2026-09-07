using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DestinationPlanner.Services;

// Persists manual per-airport corrections to airport-overrides.json, kept entirely separate
// from the OurAirports CSVs and the OpenAIP cache (openaip-airport-types.json) so neither is
// ever overwritten by a manual edit. See Models/AirportOverride and Airport.Effective*/IsHidden.
public class AirportOverrideService : IAirportOverrideService
{
    public event EventHandler? OverridesChanged;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Test seam, mirroring AppSettingsService.TestOverridePath — without it, tests running in
    // the DEBUG configuration would read/write the same airport-overrides.json a real dev build
    // uses (AppDataHelper resolves both to the same DestinationPlanner-dev folder).
    internal static string? TestOverridePath { get; set; }

    private static string FilePath =>
        TestOverridePath ?? Path.Combine(AppDataHelper.AppDataPath, "airport-overrides.json");

    private Dictionary<string, AirportOverride> _byIcao = new(StringComparer.OrdinalIgnoreCase);

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return;

            string json = await File.ReadAllTextAsync(FilePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, AirportOverride>>(json, _jsonOptions);
            if (loaded is not null)
                _byIcao = new Dictionary<string, AirportOverride>(loaded, StringComparer.OrdinalIgnoreCase);
        }
        catch { /* silently start empty if the file is missing or corrupt */ }
    }

    public void ApplyAll(IAirportDataService airports)
    {
        foreach (var (icao, o) in _byIcao)
        {
            if (airports.GetByIcao(icao) is { } airport)
                airport.Override = o;
        }
    }

    public async Task SaveOverrideAsync(Airport airport, AirportOverride? value)
    {
        if (value is null || value.IsEmpty)
        {
            _byIcao.Remove(airport.Icao);
            airport.Override = null;
        }
        else
        {
            _byIcao[airport.Icao] = value;
            airport.Override = value;
        }

        await PersistAsync();
        OverridesChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyDictionary<string, AirportOverride> GetAll() => _byIcao;

    public async Task RemoveOverrideAsync(string icao)
    {
        _byIcao.Remove(icao);
        await PersistAsync();
        OverridesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task PersistAsync()
    {
        try
        {
            AppDataHelper.EnsureCreated();
            await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(_byIcao, _jsonOptions));
        }
        catch { /* best-effort persistence, matching AppSettingsService.Save */ }
    }
}
