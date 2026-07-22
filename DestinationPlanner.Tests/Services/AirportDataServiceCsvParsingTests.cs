using DestinationPlanner.Services;
using System.IO;

namespace DestinationPlanner.Tests.Services;

public class AirportDataServiceCsvParsingTests : IDisposable
{
    private readonly string _airportsCsv = Path.Combine(Path.GetTempPath(), $"airports-{Guid.NewGuid():N}.csv");
    private readonly string _runwaysCsv = Path.Combine(Path.GetTempPath(), $"runways-{Guid.NewGuid():N}.csv");
    private readonly string _frequenciesCsv = Path.Combine(Path.GetTempPath(), $"freq-{Guid.NewGuid():N}.csv");

    private const string AirportsHeader =
        "id,ident,type,name,latitude_deg,longitude_deg,elevation_ft,continent,iso_country,iso_region,municipality,scheduled_service";

    public void Dispose()
    {
        File.Delete(_airportsCsv);
        if (File.Exists(_runwaysCsv)) File.Delete(_runwaysCsv);
        if (File.Exists(_frequenciesCsv)) File.Delete(_frequenciesCsv);
    }

    // ---- ParseAirports: instrument approach heuristic ----

    [Theory]
    [InlineData("large_airport", "no", true)]
    [InlineData("medium_airport", "no", true)]
    [InlineData("small_airport", "yes", true)]
    [InlineData("small_airport", "no", false)]
    [InlineData("heliport", "yes", false)]
    public async Task LoadAsync_InstrumentApproachHeuristic_MatchesTypeAndScheduledService(
        string type, string scheduledService, bool expectedIls)
    {
        File.WriteAllText(_airportsCsv,
            $"{AirportsHeader}\n1,ZZZZ,{type},Test Field,10.0,20.0,100,,,,,{scheduledService}\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv);

        Assert.Equal(expectedIls, service.GetByIcao("ZZZZ")!.HasInstrumentApproach);
    }

    [Fact]
    public async Task LoadAsync_RowWithTooFewFields_IsSkipped()
    {
        File.WriteAllText(_airportsCsv, $"{AirportsHeader}\n1,ZZZZ,large_airport\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv);

        Assert.Equal(0, service.Count);
    }

    [Fact]
    public async Task LoadAsync_RowWithUnparseableCoordinates_IsSkipped()
    {
        File.WriteAllText(_airportsCsv,
            $"{AirportsHeader}\n1,ZZZZ,large_airport,Test Field,not-a-number,20.0,100,,,,,no\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv);

        Assert.Equal(0, service.Count);
    }

    // ---- ApplyRunwayData ----

    [Fact]
    public async Task LoadAsync_WithRunways_SetsLongestRunwayAndSortsDescending()
    {
        File.WriteAllText(_airportsCsv, $"{AirportsHeader}\n1,ZZZZ,large_airport,Test Field,10.0,20.0,100,,,,,no\n");
        File.WriteAllText(_runwaysCsv,
            "id,airport_ref,airport_ident,length_ft,width_ft,surface,lighted,closed,le_ident,le_latitude_deg,le_longitude_deg,le_elevation_ft,le_heading_degT,le_displaced_threshold_ft,he_ident,he_latitude_deg,he_longitude_deg,he_elevation_ft,he_heading_degT,he_displaced_threshold_ft\n" +
            "1,1,ZZZZ,4000,100,ASP,1,0,09,10.0,20.0,50,90,0,27,10.01,20.01,55,270,0\n" +
            "2,1,ZZZZ,9000,150,ASP,1,0,01,10.0,20.0,50,0,0,19,10.02,20.0,55,180,0\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv, _runwaysCsv);

        var airport = service.GetByIcao("ZZZZ")!;
        Assert.Equal(9000, airport.LongestRunwayFt);
        Assert.Equal(2, airport.Runways.Count);
        Assert.Equal(9000, airport.Runways[0].LengthFt); // sorted longest-first
        Assert.Equal("01/19", airport.Runways[0].Ident);
    }

    [Fact]
    public async Task LoadAsync_ClosedRunway_IsExcluded()
    {
        File.WriteAllText(_airportsCsv, $"{AirportsHeader}\n1,ZZZZ,large_airport,Test Field,10.0,20.0,100,,,,,no\n");
        File.WriteAllText(_runwaysCsv,
            "id,airport_ref,airport_ident,length_ft,width_ft,surface,lighted,closed,le_ident,le_latitude_deg,le_longitude_deg,le_elevation_ft,le_heading_degT,le_displaced_threshold_ft,he_ident,he_latitude_deg,he_longitude_deg,he_elevation_ft,he_heading_degT,he_displaced_threshold_ft\n" +
            "1,1,ZZZZ,9000,150,ASP,1,1,01,10.0,20.0,50,0,0,19,10.02,20.0,55,180,0\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv, _runwaysCsv);

        Assert.Empty(service.GetByIcao("ZZZZ")!.Runways);
    }

    [Fact]
    public async Task LoadAsync_RunwayEndpointColumns_MapToCorrectLatLonHeading()
    {
        // Regression coverage for BUG-01: LE lat=9, lon=10, heading=12; HE lat=15, lon=16, heading=18.
        File.WriteAllText(_airportsCsv, $"{AirportsHeader}\n1,ZZZZ,large_airport,Test Field,10.0,20.0,100,,,,,no\n");
        File.WriteAllText(_runwaysCsv,
            "id,airport_ref,airport_ident,length_ft,width_ft,surface,lighted,closed,le_ident,le_latitude_deg,le_longitude_deg,le_elevation_ft,le_heading_degT,le_displaced_threshold_ft,he_ident,he_latitude_deg,he_longitude_deg,he_elevation_ft,he_heading_degT,he_displaced_threshold_ft\n" +
            "1,1,ZZZZ,6000,150,ASP,1,0,01,60.1,24.1,50,5,0,19,60.2,24.2,55,185,0\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv, _runwaysCsv);

        var rwy = service.GetByIcao("ZZZZ")!.Runways.Single();
        Assert.Equal(60.1, rwy.LeLatitude);
        Assert.Equal(24.1, rwy.LeLongitude);
        Assert.Equal(5, rwy.LeHeadingDeg);
        Assert.Equal(60.2, rwy.HeLatitude);
        Assert.Equal(24.2, rwy.HeLongitude);
        Assert.Equal(185, rwy.HeHeadingDeg);
    }

    // ---- ApplyFrequencyData ----

    [Fact]
    public async Task LoadAsync_AtisFrequency_SetsHasAtis()
    {
        File.WriteAllText(_airportsCsv, $"{AirportsHeader}\n1,ZZZZ,large_airport,Test Field,10.0,20.0,100,,,,,no\n");
        File.WriteAllText(_frequenciesCsv,
            "id,airport_ref,airport_ident,type,description,frequency_mhz\n" +
            "1,1,ZZZZ,ATIS,Test ATIS,118.5\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv, null, _frequenciesCsv);

        Assert.True(service.GetByIcao("ZZZZ")!.HasAtis);
    }

    [Fact]
    public async Task LoadAsync_NonAtisFrequency_DoesNotSetHasAtis()
    {
        File.WriteAllText(_airportsCsv, $"{AirportsHeader}\n1,ZZZZ,large_airport,Test Field,10.0,20.0,100,,,,,no\n");
        File.WriteAllText(_frequenciesCsv,
            "id,airport_ref,airport_ident,type,description,frequency_mhz\n" +
            "1,1,ZZZZ,TWR,Test Tower,118.5\n");

        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsv, null, _frequenciesCsv);

        Assert.False(service.GetByIcao("ZZZZ")!.HasAtis);
    }
}
