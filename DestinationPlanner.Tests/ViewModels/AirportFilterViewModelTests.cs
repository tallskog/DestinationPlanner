using DestinationPlanner.ViewModels;

namespace DestinationPlanner.Tests.ViewModels;

public class AirportFilterViewModelTests
{
    private static (AirportFilterViewModel Vm, Func<AirportFilterFields?> LastSaved) CreateViewModelWithSaveTracking(
        AirportFilterFields? initial = null)
    {
        AirportFilterFields? lastSaved = null;
        var vm = new AirportFilterViewModel(
            load: () => initial ?? new AirportFilterFields(),
            save: f => lastSaved = f);
        return (vm, () => lastSaved);
    }

    [Fact]
    public void Constructor_LoadsInitialValuesFromFields()
    {
        var (vm, _) = CreateViewModelWithSaveTracking(new AirportFilterFields
        {
            MinRunway = 3000,
            MaxRunway = 12000,
            UseMeters = true,
            RequireInstrumentApproach = true,
            RequireAtis = true,
            FilterCenterIcao = "EFHK",
            FilterRadiusNm = 50,
            ShowVisited = false,
            ShowNotVisited = true,
            ShowMilitaryAirports = false,
            IcaoPrefixes = "EF,ES",
        });

        Assert.Equal(3000, vm.MinRunway);
        Assert.Equal(12000, vm.MaxRunway);
        Assert.True(vm.UseMeters);
        Assert.True(vm.RequireInstrumentApproach);
        Assert.True(vm.RequireAtis);
        Assert.Equal("EFHK", vm.FilterCenterIcao);
        Assert.Equal(50, vm.FilterRadiusNm);
        Assert.False(vm.ShowVisited);
        Assert.True(vm.ShowNotVisited);
        Assert.False(vm.ShowMilitaryAirports);
        Assert.Equal("EF,ES", vm.IcaoPrefixes);
    }

    [Fact]
    public void BuildCriteria_ConvertsMetersToFeetWhenUseMetersIsTrue()
    {
        var (vm, _) = CreateViewModelWithSaveTracking();
        vm.UseMeters = true;
        vm.MinRunway = 1000; // meters
        vm.MaxRunway = 3000; // meters

        var criteria = vm.BuildCriteria();

        Assert.Equal(3281, criteria.MinRunwayFt);
        Assert.Equal(9843, criteria.MaxRunwayFt);
    }

    [Fact]
    public void BuildCriteria_UsesRawFeetWhenUseMetersIsFalse()
    {
        var (vm, _) = CreateViewModelWithSaveTracking();
        vm.MinRunway = 3000;
        vm.MaxRunway = 9000;

        var criteria = vm.BuildCriteria();

        Assert.Equal(3000, criteria.MinRunwayFt);
        Assert.Equal(9000, criteria.MaxRunwayFt);
    }

    // Behavior difference from the old Map-only BuildFilterCriteria this replaced: that method
    // omitted FilterCenterIcao/FilterRadiusNm because MapViewModel applies center-radius itself
    // as a separate pre-filter step. This shared BuildCriteria() must always include them, since
    // ITripCandidateService.GetCandidates reads them directly off the criteria it's given.
    [Fact]
    public void BuildCriteria_PopulatesFilterCenterIcaoAndRadius()
    {
        var (vm, _) = CreateViewModelWithSaveTracking();
        vm.FilterCenterIcao = "EFHK";
        vm.FilterRadiusNm = 150;

        var criteria = vm.BuildCriteria();

        Assert.Equal("EFHK", criteria.FilterCenterIcao);
        Assert.Equal(150, criteria.FilterRadiusNm);
    }

    [Fact]
    public void BuildCriteria_BlankFilterCenterIcao_MapsToNull()
    {
        var (vm, _) = CreateViewModelWithSaveTracking();
        vm.FilterCenterIcao = "   ";

        var criteria = vm.BuildCriteria();

        Assert.Null(criteria.FilterCenterIcao);
    }

    [Fact]
    public void BuildCriteria_ParsesIcaoPrefixes()
    {
        var (vm, _) = CreateViewModelWithSaveTracking();
        vm.IcaoPrefixes = "EF, ES;LF";

        var criteria = vm.BuildCriteria();

        Assert.Equal(["EF", "ES", "LF"], criteria.IcaoPrefixes);
    }

    [Fact]
    public void ApplyFiltersCommand_PersistsCurrentValuesAndRaisesChanged()
    {
        var (vm, lastSaved) = CreateViewModelWithSaveTracking();
        vm.MinRunway = 5000;
        vm.ShowMilitaryAirports = false;
        bool changedRaised = false;
        vm.Changed += (_, _) => changedRaised = true;

        vm.ApplyFiltersCommand.Execute(null);

        Assert.Equal(5000, lastSaved()!.MinRunway);
        Assert.False(lastSaved()!.ShowMilitaryAirports);
        Assert.True(changedRaised);
    }

    [Fact]
    public void ClearFiltersCommand_ResetsEveryFieldToDefaultAndPersists()
    {
        var (vm, lastSaved) = CreateViewModelWithSaveTracking();
        vm.MinRunway = 3000;
        vm.MaxRunway = 9000;
        vm.UseMeters = true;
        vm.RequireInstrumentApproach = true;
        vm.RequireAtis = true;
        vm.FilterCenterIcao = "EFHK";
        vm.FilterRadiusNm = 100;
        vm.ShowVisited = false;
        vm.ShowNotVisited = false;
        vm.ShowMilitaryAirports = false;
        vm.ShowHeliportAirports = false;
        vm.ShowPrivateAirports = false;
        vm.ShowOtherAirports = false;
        vm.ShowUnknownAirports = false;
        vm.ShowUnclassifiedAirports = false;
        vm.IcaoPrefixes = "EF";
        bool changedRaised = false;
        vm.Changed += (_, _) => changedRaised = true;

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(0, vm.MinRunway);
        Assert.Equal(0, vm.MaxRunway);
        Assert.False(vm.UseMeters);
        Assert.False(vm.RequireInstrumentApproach);
        Assert.False(vm.RequireAtis);
        Assert.Equal(string.Empty, vm.FilterCenterIcao);
        Assert.Equal(0, vm.FilterRadiusNm);
        Assert.True(vm.ShowVisited);
        Assert.True(vm.ShowNotVisited);
        Assert.True(vm.ShowCivilAirports);
        Assert.True(vm.ShowMilitaryAirports);
        Assert.True(vm.ShowHeliportAirports);
        Assert.True(vm.ShowPrivateAirports);
        Assert.True(vm.ShowOtherAirports);
        Assert.True(vm.ShowUnknownAirports);
        Assert.True(vm.ShowUnclassifiedAirports);
        Assert.Equal(string.Empty, vm.IcaoPrefixes);
        Assert.Equal(0, lastSaved()!.MinRunway);
        Assert.True(changedRaised);
    }

    [Fact]
    public void Persist_SavesWithoutRaisingChanged()
    {
        var (vm, lastSaved) = CreateViewModelWithSaveTracking();
        vm.MinRunway = 4000;
        bool changedRaised = false;
        vm.Changed += (_, _) => changedRaised = true;

        vm.Persist();

        Assert.Equal(4000, lastSaved()!.MinRunway);
        Assert.False(changedRaised);
    }
}
