using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using DestinationPlanner.Services;
using DestinationPlanner.Tests.Fakes;
using DestinationPlanner.ViewModels;
using System.IO;

namespace DestinationPlanner.Tests.ViewModels;

// IDisposable: TripPlanViewModel persists via TripPlanStore.SaveAll, and (as of US43) its
// Filters (AirportFilterViewModel) persists via AppSettingsService.Save. Without redirecting
// both to throwaway files, tests would overwrite the real dev tripplans.local.json/settings.json
// (AppDataHelper resolves to the same DEBUG AppData folder used by a real dev build — see
// CLAUDE.md BUG-06).
public class TripPlanViewModelTests : IDisposable
{
    private readonly string _tempStorePath =
        Path.Combine(Path.GetTempPath(), $"dp-test-tripplans-{Guid.NewGuid():N}.json");
    private readonly string _tempSettingsPath =
        Path.Combine(Path.GetTempPath(), $"dp-test-settings-{Guid.NewGuid():N}.json");

    public TripPlanViewModelTests()
    {
        TripPlanStore.TestOverridePath = _tempStorePath;
        AppSettingsService.TestOverridePath = _tempSettingsPath;
    }

    public void Dispose()
    {
        TripPlanStore.TestOverridePath = null;
        AppSettingsService.TestOverridePath = null;
        try { File.Delete(_tempStorePath); } catch { /* best-effort cleanup */ }
        try { File.Delete(_tempSettingsPath); } catch { /* best-effort cleanup */ }
    }

    private static FakeAirportDataService CreateAirports() => new(new[]
    {
        new Airport { Icao = "EFHK", Name = "Helsinki",   Latitude = 60.3, Longitude = 24.9, LongestRunwayFt = 10000 },
        new Airport { Icao = "ENGM", Name = "Oslo",       Latitude = 60.2, Longitude = 11.1, LongestRunwayFt = 9700 },
    });

    private static TripPlanViewModel CreateViewModel(
        out FakeAiTripPlanningService fakeAi, IAirportDataService? airports = null, bool aiConfigured = true,
        ILogbookService? logbook = null, AppSettings? settings = null)
    {
        var data = airports ?? CreateAirports();
        logbook ??= new FakeLogbookService();
        var candidates = new TripCandidateService(data, logbook);
        fakeAi = new FakeAiTripPlanningService();
        var ai = fakeAi;
        return new TripPlanViewModel(data, candidates, logbook, settings ?? new AppSettings(), () => aiConfigured, () => ai);
    }

    [Fact]
    public void GenerateCommand_CanExecute_FalseWhenQueryEmpty()
    {
        var vm = CreateViewModel(out _);

        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateCommand_CanExecute_FalseWhenAiNotConfigured()
    {
        var vm = CreateViewModel(out _, aiConfigured: false);
        vm.QueryText = "airports in the nordics";

        Assert.False(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void GenerateCommand_CanExecute_TrueWhenConfiguredAndQueryPresent()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports in the nordics";

        Assert.True(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public async Task GenerateAsync_PopulatesCandidatesFromDeterministicFilter()
    {
        var vm = CreateViewModel(out var fakeAi);
        vm.QueryText = "airports over 9000ft";
        fakeAi.QueryFiltersToReturn = new TripQueryFilters
        {
            MinRunwayFt = 9000,
            IntentSummary = "Airports over 9000ft",
        };

        await vm.GenerateAsync();

        Assert.Equal(1, fakeAi.ParseQueryCallCount);
        Assert.Equal(["EFHK", "ENGM"], vm.Candidates.Select(a => a.Icao).OrderBy(x => x));
        Assert.Contains("Airports over 9000ft", vm.StatusText);
    }

    [Fact]
    public async Task GenerateAsync_NoMatches_SetsStatusAndEmptyCandidates()
    {
        var vm = CreateViewModel(out var fakeAi);
        vm.QueryText = "airports over 50000ft";
        fakeAi.QueryFiltersToReturn = new TripQueryFilters { MinRunwayFt = 50000, IntentSummary = "none" };

        await vm.GenerateAsync();

        Assert.Empty(vm.Candidates);
        Assert.Contains("No matching airports", vm.StatusText);
    }

    [Fact]
    public async Task ConfirmAsync_SavesGeneratedPlanAndSelectsIt()
    {
        var vm = CreateViewModel(out var fakeAi);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();

        await vm.ConfirmAsync();

        Assert.Equal(1, fakeAi.PlanTripCallCount);
        Assert.Single(vm.SavedPlans);
        Assert.Same(vm.SavedPlans[0], vm.SelectedPlan);
        Assert.Single(vm.SelectedPlanLegs);
    }

    [Fact]
    public async Task ConfirmAsync_RecordsTheQueryOnTheSavedPlan()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports over 9000ft in the nordics";
        await vm.GenerateAsync();

        await vm.ConfirmAsync();

        Assert.Equal("airports over 9000ft in the nordics", vm.SavedPlans[0].Query);
    }

    [Fact]
    public async Task ConfirmAsync_PersistsAcrossNewViewModelInstance()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();

        var reloaded = CreateViewModel(out _);

        Assert.Single(reloaded.SavedPlans);
        Assert.Equal(vm.SavedPlans[0].Title, reloaded.SavedPlans[0].Title);
    }

    [Fact]
    public async Task MarkLegFlownCommand_MarksSelectedLegAndPersists()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();
        vm.SelectedLeg = vm.SelectedPlanLegs[0];

        vm.MarkLegFlownCommand.Execute(null);

        Assert.Equal(TripLegStatus.Flown, vm.SelectedPlanLegs[0].Status);

        var reloaded = CreateViewModel(out _);
        Assert.Equal(TripLegStatus.Flown, reloaded.SavedPlans[0].Legs[0].Status);
    }

    [Fact]
    public void MarkLegFlownCommand_CanExecute_FalseWhenNoLegSelected()
    {
        var vm = CreateViewModel(out _);

        Assert.False(vm.MarkLegFlownCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoggingAMatchingFlight_AutoMarksTheLegFlownAndPersists()
    {
        var logbook = new FakeLogbookService();
        var vm = CreateViewModel(out _, logbook: logbook);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();
        var leg = vm.SavedPlans[0].Legs[0];
        Assert.Equal(TripLegStatus.Planned, leg.Status);

        var flight = new FlightRecord { DepartureIcao = leg.DepartureIcao, ArrivalIcao = leg.ArrivalIcao };
        logbook.AddFlight(flight);

        Assert.Equal(TripLegStatus.Flown, leg.Status);
        Assert.Equal(flight.Id, leg.FlownFlightId);
        Assert.Equal(TripLegStatus.Flown, vm.SelectedPlanLegs[0].Status);

        var reloaded = CreateViewModel(out _, logbook: logbook);
        Assert.Equal(TripLegStatus.Flown, reloaded.SavedPlans[0].Legs[0].Status);
    }

    [Fact]
    public async Task LoggingANonMatchingFlight_LeavesLegPlanned()
    {
        var logbook = new FakeLogbookService();
        var vm = CreateViewModel(out _, logbook: logbook);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();

        logbook.AddFlight(new FlightRecord { DepartureIcao = "EFHK", ArrivalIcao = "EFHK" });

        Assert.Equal(TripLegStatus.Planned, vm.SavedPlans[0].Legs[0].Status);
    }

    [Fact]
    public async Task AlreadyLoggedFlight_AutoMarksLegFlownOnConstruction()
    {
        var logbook = new FakeLogbookService();
        var vm = CreateViewModel(out _, logbook: logbook);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();
        var leg = vm.SavedPlans[0].Legs[0];
        logbook.SetFlights([new FlightRecord { DepartureIcao = leg.DepartureIcao, ArrivalIcao = leg.ArrivalIcao }]);
        // Reset the leg back to Planned in the persisted file (simulating a flight that was
        // already logged before this plan-reload picks it up), bypassing the ViewModel so the
        // save doesn't itself trigger the auto-mark we're trying to test on construction.
        leg.Status = TripLegStatus.Planned;
        TripPlanStore.SaveAll(vm.SavedPlans);

        var reloaded = CreateViewModel(out _, logbook: logbook);

        Assert.Equal(TripLegStatus.Flown, reloaded.SavedPlans[0].Legs[0].Status);
    }

    [Fact]
    public void RemoveSelectedCandidateCommand_CanExecute_FalseWhenNoCandidateSelected()
    {
        var vm = CreateViewModel(out _);

        Assert.False(vm.RemoveSelectedCandidateCommand.CanExecute(null));
    }

    [Fact]
    public async Task RemoveSelectedCandidateCommand_RemovesOnlySelectedCandidate()
    {
        var vm = CreateViewModel(out var fakeAi);
        vm.QueryText = "airports over 9000ft";
        fakeAi.QueryFiltersToReturn = new TripQueryFilters { MinRunwayFt = 9000, IntentSummary = "test" };
        await vm.GenerateAsync();
        vm.SelectedCandidate = vm.Candidates.Single(a => a.Icao == "EFHK");

        vm.RemoveSelectedCandidateCommand.Execute(null);

        Assert.Single(vm.Candidates);
        Assert.Equal("ENGM", vm.Candidates[0].Icao);
        Assert.Null(vm.SelectedCandidate);
    }

    [Fact]
    public void DeleteSelectedPlanCommand_CanExecute_FalseWhenNoPlanSelected()
    {
        var vm = CreateViewModel(out _);

        Assert.False(vm.DeleteSelectedPlanCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteSelectedPlanCommand_RemovesPlanAndPersists()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();

        vm.DeleteSelectedPlanCommand.Execute(null);

        Assert.Empty(vm.SavedPlans);
        Assert.Null(vm.SelectedPlan);
        Assert.Empty(vm.SelectedPlanLegs);

        var reloaded = CreateViewModel(out _);
        Assert.Empty(reloaded.SavedPlans);
    }

    [Fact]
    public async Task SelectedPlanLegs_ComputesDistanceBetweenLegAirports()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();

        var expectedNm = GeoHelper.DistanceNm(60.3, 24.9, 60.2, 11.1); // EFHK -> ENGM
        Assert.NotNull(vm.SelectedPlanLegs[0].DistanceNm);
        Assert.Equal(expectedNm, vm.SelectedPlanLegs[0].DistanceNm!.Value, precision: 3);
    }

    [Fact]
    public async Task SelectedPlanLegs_UnknownAirport_DistanceIsNull()
    {
        var airports = new FakeAirportDataService(new[]
        {
            new Airport { Icao = "EFHK", Name = "Helsinki", Latitude = 60.3, Longitude = 24.9, LongestRunwayFt = 10000 },
        });
        var vm = CreateViewModel(out var fakeAi, airports);
        vm.QueryText = "airports over 9000ft";
        await vm.GenerateAsync(); // only EFHK is in the loaded dataset
        fakeAi.PlanFactory = (candidates, _, _) => new TripPlan
        {
            Title = "Test",
            Narrative = "Test",
            Legs = [new TripLeg { Order = 1, DepartureIcao = "EFHK", ArrivalIcao = "ENGM" }], // ENGM unknown
        };

        await vm.ConfirmAsync();

        Assert.Null(vm.SelectedPlanLegs[0].DistanceNm);
    }

    // Roughly evenly spaced along a line of longitude at 60N — consecutive distances are
    // predictable and increase with index (~90nm apart), matching TripRouteBuilderTests.
    private static FakeAirportDataService CreateSpacedAirports() => new(new[]
    {
        new Airport { Icao = "AAAA", Latitude = 60.0, Longitude = 0.0 },
        new Airport { Icao = "BBBB", Latitude = 60.0, Longitude = 3.0 },
        new Airport { Icao = "CCCC", Latitude = 60.0, Longitude = 6.0 },
        new Airport { Icao = "DDDD", Latitude = 60.0, Longitude = 9.0 },
    });

    [Fact]
    public async Task ConfirmAsync_WithLegDistanceBounds_UsesDeterministicRouteInsteadOfPlanTripAsync()
    {
        var vm = CreateViewModel(out var fakeAi, CreateSpacedAirports());
        vm.QueryText = "airports with legs around 90nm";
        fakeAi.QueryFiltersToReturn = new TripQueryFilters { MinLegDistanceNm = 0, MaxLegDistanceNm = 1000, IntentSummary = "test" };
        await vm.GenerateAsync();

        await vm.ConfirmAsync();

        Assert.Equal(0, fakeAi.PlanTripCallCount);
        Assert.Equal(1, fakeAi.NarrateCallCount);
        Assert.Single(vm.SavedPlans);
        var legs = vm.SavedPlans[0].Legs;
        Assert.Equal(3, legs.Count);
        Assert.All(legs, l =>
        {
            var from = CreateSpacedAirports().GetByIcao(l.DepartureIcao)!;
            var to = CreateSpacedAirports().GetByIcao(l.ArrivalIcao)!;
            var dist = GeoHelper.DistanceNm(from.Latitude, from.Longitude, to.Latitude, to.Longitude);
            Assert.InRange(dist, 0, 1000);
        });
        Assert.Equal("airports with legs around 90nm", vm.SavedPlans[0].Query);
    }

    [Fact]
    public async Task ConfirmAsync_WithLegDistanceBounds_NoValidRoute_DoesNotSaveAndExplainsWhy()
    {
        var vm = CreateViewModel(out var fakeAi, CreateSpacedAirports());
        vm.QueryText = "airports with legs around 5nm"; // far tighter than the ~90nm spacing
        fakeAi.QueryFiltersToReturn = new TripQueryFilters { MinLegDistanceNm = 0, MaxLegDistanceNm = 5, IntentSummary = "test" };
        await vm.GenerateAsync();

        await vm.ConfirmAsync();

        Assert.Empty(vm.SavedPlans);
        Assert.Equal(0, fakeAi.NarrateCallCount);
        Assert.Contains("No valid route", vm.StatusText);
    }

    [Fact]
    public async Task ConfirmAsync_WithLegDistanceBounds_SkippedCandidates_NotedInStatus()
    {
        // A/B/C are close together (~45nm apart); D is placed far enough away that it can
        // never join the chain within the 100nm bound below — exactly one candidate skipped.
        var airports = new FakeAirportDataService(new[]
        {
            new Airport { Icao = "AAAA", Latitude = 60.0, Longitude = 0.0 },
            new Airport { Icao = "BBBB", Latitude = 60.0, Longitude = 1.5 },
            new Airport { Icao = "CCCC", Latitude = 60.0, Longitude = 3.0 },
            new Airport { Icao = "DDDD", Latitude = 65.0, Longitude = 30.0 },
        });
        var vm = CreateViewModel(out var fakeAi, airports);
        vm.QueryText = "airports with legs around 45nm";
        fakeAi.QueryFiltersToReturn = new TripQueryFilters { MinLegDistanceNm = 0, MaxLegDistanceNm = 100, IntentSummary = "test" };
        await vm.GenerateAsync();

        await vm.ConfirmAsync();

        Assert.Single(vm.SavedPlans);
        Assert.Equal(2, vm.SavedPlans[0].Legs.Count); // A-B-C connected; D left out
        Assert.Contains("could not be connected", vm.StatusText);
    }

    // ---- Explicit Min/Max leg (nm) fields (US43 follow-up) ----

    [Fact]
    public async Task ConfirmAsync_ExplicitMaxLegNm_UsesDeterministicRouteWithoutGenerateEverRunning()
    {
        var airports = CreateSpacedAirports();
        var vm = CreateViewModel(out var fakeAi, airports);
        // Candidates built manually (e.g. via filters/search) — GenerateAsync is never called,
        // so _lastMinLegNm/_lastMaxLegNm stay at their default 0.
        foreach (var a in airports.GetAll()) vm.Candidates.Add(a);
        vm.MaxLegNm = 1000;

        await vm.ConfirmAsync();

        Assert.Equal(0, fakeAi.PlanTripCallCount);
        Assert.Equal(1, fakeAi.NarrateCallCount);
        Assert.Single(vm.SavedPlans);
        Assert.Equal(3, vm.SavedPlans[0].Legs.Count);
    }

    [Fact]
    public async Task ConfirmAsync_ExplicitMaxLegNm_TakesPrecedenceOverAiParsedValue()
    {
        var airports = CreateSpacedAirports();
        var vm = CreateViewModel(out var fakeAi, airports);
        vm.QueryText = "airports with legs around 5nm"; // far tighter than the ~90nm spacing
        fakeAi.QueryFiltersToReturn = new TripQueryFilters { MinLegDistanceNm = 0, MaxLegDistanceNm = 5, IntentSummary = "test" };
        await vm.GenerateAsync(); // parses MaxLegDistanceNm=5 into _lastMaxLegNm — would fail alone

        vm.MaxLegNm = 1000; // explicit field overrides the AI-parsed 5nm bound

        await vm.ConfirmAsync();

        Assert.Single(vm.SavedPlans);
        Assert.Equal(3, vm.SavedPlans[0].Legs.Count);
        Assert.DoesNotContain("No valid route", vm.StatusText);
    }

    [Fact]
    public void ReuseQueryCommand_CanExecute_FalseWhenNoPlanSelected()
    {
        var vm = CreateViewModel(out _);

        Assert.False(vm.ReuseQueryCommand.CanExecute(null));
    }

    [Fact]
    public async Task ReuseQueryCommand_CopiesSelectedPlanQueryIntoQueryText()
    {
        var vm = CreateViewModel(out _);
        vm.QueryText = "airports over 9000ft in the nordics";
        await vm.GenerateAsync();
        await vm.ConfirmAsync();
        vm.QueryText = "something else entirely"; // simulate the user having typed a new query

        vm.ReuseQueryCommand.Execute(null);

        Assert.Equal("airports over 9000ft in the nordics", vm.QueryText);
    }

    // ---- US43: manual filters, direct search, candidate review map ----

    [Fact]
    public void AddFilteredAirports_AddsMatchingAirportsToEmptyCandidates()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.MinRunway = 9000;

        vm.AddFilteredAirports();

        Assert.Equal(["EFHK", "ENGM"], vm.Candidates.Select(a => a.Icao).OrderBy(x => x));
    }

    [Fact]
    public void AddFilteredAirports_MergesWithoutDuplicatingExistingCandidate()
    {
        var vm = CreateViewModel(out _);
        var existing = CreateAirports().GetByIcao("EFHK")!;
        vm.Candidates.Add(existing);
        vm.Filters.MinRunway = 9000; // matches both EFHK and ENGM

        vm.AddFilteredAirports();

        Assert.Equal(2, vm.Candidates.Count);
        Assert.Single(vm.Candidates, a => a.Icao == "EFHK");
        Assert.Single(vm.Candidates, a => a.Icao == "ENGM");
        Assert.Contains("1 airport(s)", vm.StatusText);
        Assert.Contains("1 already present", vm.StatusText);
    }

    [Fact]
    public void AddFilteredAirports_BothVisitStatusUnchecked_NoOpsWithStatusMessage()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.ShowVisited = false;
        vm.Filters.ShowNotVisited = false;

        vm.AddFilteredAirports();

        Assert.Empty(vm.Candidates);
        Assert.Contains("both visit-status checkboxes are unchecked", vm.StatusText);
    }

    [Fact]
    public void AddFilteredAirports_ExcludesVisitedWhenShowVisitedUnchecked()
    {
        var logbook = new FakeLogbookService();
        logbook.SetFlights([new FlightRecord { DepartureIcao = "EFHK", ArrivalIcao = "ENGM" }]);
        var vm = CreateViewModel(out _, logbook: logbook);
        vm.Filters.ShowVisited = false;

        vm.AddFilteredAirports();

        Assert.Empty(vm.Candidates); // both airports are visited (used in the one logged flight)
    }

    [Fact]
    public void AddFilteredAirports_PersistsFilterValues()
    {
        var vm = CreateViewModel(out _);
        vm.Filters.MinRunway = 7500;

        vm.AddFilteredAirports();

        // Re-read via a fresh AppSettings load from the same (redirected) settings path.
        var settings = AppSettingsService.Load();
        Assert.Equal(7500, settings.TripPlanMinRunway);
    }

    [Fact]
    public void AddCandidateAirport_AddsNewAirport()
    {
        var vm = CreateViewModel(out _);
        var airport = CreateAirports().GetByIcao("EFHK")!;

        vm.AddCandidateAirport(airport);

        Assert.Single(vm.Candidates);
        Assert.Equal("EFHK", vm.Candidates[0].Icao);
    }

    [Fact]
    public void AddCandidateAirport_SkipsDuplicateIcaoCaseInsensitive()
    {
        var vm = CreateViewModel(out _);
        var airport = CreateAirports().GetByIcao("EFHK")!;
        vm.AddCandidateAirport(airport);

        vm.AddCandidateAirport(new Airport { Icao = "efhk", Name = "Duplicate", Latitude = 0, Longitude = 0 });

        Assert.Single(vm.Candidates);
    }

    [Fact]
    public void AddCandidateAirport_ClearsCandidateSearchText()
    {
        var vm = CreateViewModel(out _);
        vm.CandidateSearchText = "EFH";
        var airport = CreateAirports().GetByIcao("EFHK")!;

        vm.AddCandidateAirport(airport);

        Assert.Equal(string.Empty, vm.CandidateSearchText);
    }

    [Fact]
    public void CandidateSearchResults_MatchesByIcaoPrefix()
    {
        var vm = CreateViewModel(out _);

        vm.CandidateSearchText = "EF";

        Assert.Contains(vm.CandidateSearchResults, a => a.Icao == "EFHK");
        Assert.DoesNotContain(vm.CandidateSearchResults, a => a.Icao == "ENGM");
    }

    [Fact]
    public void CandidateSearchResults_MatchesByNameContains()
    {
        var vm = CreateViewModel(out _);

        vm.CandidateSearchText = "sinki";

        Assert.Contains(vm.CandidateSearchResults, a => a.Icao == "EFHK");
    }

    [Fact]
    public void CandidateSearchResults_EmptyQuery_ReturnsNoResults()
    {
        var vm = CreateViewModel(out _);

        vm.CandidateSearchText = "";

        Assert.Empty(vm.CandidateSearchResults);
    }
}
