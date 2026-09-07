using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using System.IO;

namespace DestinationPlanner.Tests.Services;

// IDisposable: AirportOverrideService writes to AppDataHelper.AppDataPath by default, which
// resolves to the same DestinationPlanner-dev folder a real dev build uses — redirect every
// test to a throwaway file via TestOverridePath so a test run never touches real user data
// (same rationale as AppSettingsService.TestOverridePath; see CLAUDE.md's data-safety rule).
public class AirportOverrideServiceTests : IDisposable
{
    private readonly string _tempPath =
        Path.Combine(Path.GetTempPath(), $"dp-test-airport-overrides-{Guid.NewGuid():N}.json");

    public AirportOverrideServiceTests()
    {
        AirportOverrideService.TestOverridePath = _tempPath;
    }

    public void Dispose()
    {
        AirportOverrideService.TestOverridePath = null;
        try { File.Delete(_tempPath); } catch { /* best-effort cleanup */ }
    }

    private static FakeAirportDataService CreateAirports() => new(new[]
    {
        new Airport { Icao = "EFHK", Name = "Helsinki-Vantaa", Latitude = 60.3, Longitude = 24.9, Type = AirportType.Civil },
        new Airport { Icao = "EFTU", Name = "Turku",           Latitude = 60.5, Longitude = 22.2, Type = AirportType.Military },
    });

    [Fact]
    public async Task LoadAsync_NoFileYet_StartsEmptyWithoutThrowing()
    {
        var service = new AirportOverrideService();

        await service.LoadAsync();
        service.ApplyAll(CreateAirports());
        // No exception is the assertion here.
    }

    [Fact]
    public async Task SaveOverrideAsync_ThenApplyAll_StampsOverrideOntoMatchingAirport()
    {
        var airports = CreateAirports();
        var service = new AirportOverrideService();
        var airport = airports.GetByIcao("EFHK")!;

        await service.SaveOverrideAsync(airport, new AirportOverride { Type = AirportType.Military });

        Assert.Equal(AirportType.Military, airport.EffectiveType);
    }

    [Fact]
    public async Task SaveOverrideAsync_PersistsToDisk_AndReloadsOnANewInstance()
    {
        var airports = CreateAirports();
        var writer = new AirportOverrideService();
        await writer.SaveOverrideAsync(airports.GetByIcao("EFHK")!, new AirportOverride { IsHidden = true });

        // Simulate a fresh app start: new service instance, freshly-loaded (new) Airport objects.
        var reader = new AirportOverrideService();
        await reader.LoadAsync();
        var freshAirports = CreateAirports();
        reader.ApplyAll(freshAirports);

        Assert.True(freshAirports.GetByIcao("EFHK")!.IsHidden);
        Assert.False(freshAirports.GetByIcao("EFTU")!.IsHidden);
    }

    [Fact]
    public async Task SaveOverrideAsync_EmptyOverride_RemovesAnyExistingEntryInsteadOfPersistingIt()
    {
        var airports = CreateAirports();
        var airport = airports.GetByIcao("EFHK")!;
        var service = new AirportOverrideService();
        await service.SaveOverrideAsync(airport, new AirportOverride { Type = AirportType.Military });

        // Clearing every field (an "empty" override) should reset the airport and remove the
        // persisted entry entirely, not write a meaningless all-null record.
        await service.SaveOverrideAsync(airport, new AirportOverride());

        Assert.Null(airport.Override);

        var reader = new AirportOverrideService();
        await reader.LoadAsync();
        var freshAirports = CreateAirports();
        reader.ApplyAll(freshAirports);
        Assert.Null(freshAirports.GetByIcao("EFHK")!.Override);
    }

    [Fact]
    public async Task SaveOverrideAsync_NullValue_ClearsExistingOverride()
    {
        var airports = CreateAirports();
        var airport = airports.GetByIcao("EFHK")!;
        var service = new AirportOverrideService();
        await service.SaveOverrideAsync(airport, new AirportOverride { Name = "Custom Name" });

        await service.SaveOverrideAsync(airport, null);

        Assert.Null(airport.Override);
    }

    [Fact]
    public async Task ApplyAll_OnlyTouchesAirportsWithAPersistedOverride()
    {
        var airports = CreateAirports();
        var service = new AirportOverrideService();
        await service.SaveOverrideAsync(airports.GetByIcao("EFHK")!, new AirportOverride { Type = AirportType.Private });

        var freshAirports = CreateAirports();
        service.ApplyAll(freshAirports);

        Assert.Equal(AirportType.Private, freshAirports.GetByIcao("EFHK")!.EffectiveType);
        Assert.Null(freshAirports.GetByIcao("EFTU")!.Override);
    }

    [Fact]
    public async Task GetAll_ReturnsEveryPersistedOverrideKeyedByIcao()
    {
        var airports = CreateAirports();
        var service = new AirportOverrideService();
        await service.SaveOverrideAsync(airports.GetByIcao("EFHK")!, new AirportOverride { IsHidden = true });
        await service.SaveOverrideAsync(airports.GetByIcao("EFTU")!, new AirportOverride { Type = AirportType.Civil });

        var all = service.GetAll();

        Assert.Equal(2, all.Count);
        Assert.True(all["EFHK"].IsHidden);
        Assert.Equal(AirportType.Civil, all["EFTU"].Type);
    }

    [Fact]
    public async Task RemoveOverrideAsync_ByIcaoAlone_RemovesItAndPersists()
    {
        var airports = CreateAirports();
        var service = new AirportOverrideService();
        await service.SaveOverrideAsync(airports.GetByIcao("EFHK")!, new AirportOverride { IsHidden = true });

        // Simulates the "manage overrides" window clearing an entry for an airport that isn't
        // currently loaded (no live Airport instance available to pass to SaveOverrideAsync).
        await service.RemoveOverrideAsync("EFHK");

        Assert.Empty(service.GetAll());

        var reader = new AirportOverrideService();
        await reader.LoadAsync();
        Assert.Empty(reader.GetAll());
    }

    [Fact]
    public async Task SaveOverrideAsync_RaisesOverridesChanged_SoAnyOpenWindowCanRefreshImmediately()
    {
        var airports = CreateAirports();
        var service = new AirportOverrideService();
        int raisedCount = 0;
        service.OverridesChanged += (_, _) => raisedCount++;

        await service.SaveOverrideAsync(airports.GetByIcao("EFHK")!, new AirportOverride { IsHidden = true });

        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public async Task RemoveOverrideAsync_RaisesOverridesChanged()
    {
        var airports = CreateAirports();
        var service = new AirportOverrideService();
        await service.SaveOverrideAsync(airports.GetByIcao("EFHK")!, new AirportOverride { IsHidden = true });
        int raisedCount = 0;
        service.OverridesChanged += (_, _) => raisedCount++;

        await service.RemoveOverrideAsync("EFHK");

        Assert.Equal(1, raisedCount);
    }
}
