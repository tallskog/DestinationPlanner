using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface IAirportOverrideService
{
    // Raised whenever an override is saved or removed, from any window — lets every currently
    // open map surface (and the "manage overrides" list) refresh immediately, regardless of
    // where the edit happened, instead of only the window that triggered it.
    event EventHandler? OverridesChanged;

    // Reads the persisted overrides file into memory. Silently starts empty if the file is
    // missing or corrupt — no override ever blocks the app from starting.
    Task LoadAsync();

    // Stamps airport.Override onto every airport in the given dataset from the in-memory
    // overrides loaded by LoadAsync/SaveOverrideAsync. Call after every fresh CSV load
    // (AirportDataService.LoadAsync rebuilds Airport objects from scratch, resetting Override
    // to null on each).
    void ApplyAll(IAirportDataService airports);

    // Upserts (or, if value is null/IsEmpty, removes) the override for this airport, persists
    // the whole overrides file immediately, and sets airport.Override in-place so the change
    // is reflected live without needing a reload.
    Task SaveOverrideAsync(Airport airport, AirportOverride? value);

    // Every currently-saved override, keyed by ICAO — for the "manage overrides" list window.
    IReadOnlyDictionary<string, AirportOverride> GetAll();

    // Removes an override by ICAO alone, for when the airport isn't in the currently loaded
    // dataset (so there's no live Airport instance to pass to SaveOverrideAsync).
    Task RemoveOverrideAsync(string icao);
}
