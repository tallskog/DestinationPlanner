using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// In-memory-only session bookkeeping. The access token must never be written to
// disk (short-lived; re-acquired via the persisted refresh token instead) — see
// Helpers.NavigraphTokenStore for the durable, DPAPI-encrypted refresh-token cache.
public class NavigraphSessionState
{
    public string? AccessToken { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    // Re-applied after every AirportDataService.LoadAsync call, since LoadAsync
    // rebuilds the airport dictionary from scratch and would otherwise wipe classifications.
    public IReadOnlyDictionary<string, AirportType>? LastAppliedTypesByIcao { get; set; }

    public bool HasValidAccessToken =>
        AccessToken is not null && AccessTokenExpiresAtUtc is { } exp && exp > DateTime.UtcNow.AddMinutes(2);
}
