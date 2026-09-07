using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface IAirportOverrideService
{
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
}
