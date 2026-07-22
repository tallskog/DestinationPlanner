using DestinationPlanner.Models;
using DestinationPlanner.Services;
using System.IO;

namespace DestinationPlanner.Tests.Services;

public class AirportDataServiceTests : IDisposable
{
    private readonly string _airportsCsvPath = Path.Combine(Path.GetTempPath(), $"airports-{Guid.NewGuid():N}.csv");

    public AirportDataServiceTests()
    {
        File.WriteAllText(_airportsCsvPath,
            "id,ident,type,name,latitude_deg,longitude_deg,elevation_ft,continent,iso_country,iso_region,municipality,scheduled_service\n" +
            "1,EFHK,large_airport,Helsinki-Vantaa Airport,60.3172,24.9633,179,EU,FI,FI-18,Helsinki,yes\n" +
            "2,EFTU,medium_airport,Turku Airport,60.5142,22.2627,49,EU,FI,FI-19,Turku,yes\n" +
            "3,ZZZZ,small_airport,Nowhere Field,10.0,20.0,100,,,,,\n");
    }

    public void Dispose() => File.Delete(_airportsCsvPath);

    [Fact]
    public async Task ApplyAirportTypes_SetsTypeOnMatchedIcaos()
    {
        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsvPath);

        service.ApplyAirportTypes(new Dictionary<string, AirportType>
        {
            ["EFHK"] = AirportType.Civil,
            ["EFTU"] = AirportType.Military,
        });

        Assert.Equal(AirportType.Civil, service.GetByIcao("EFHK")!.Type);
        Assert.Equal(AirportType.Military, service.GetByIcao("EFTU")!.Type);
    }

    [Fact]
    public async Task ApplyAirportTypes_LeavesUnmatchedIcaosUnclassified()
    {
        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsvPath);

        service.ApplyAirportTypes(new Dictionary<string, AirportType> { ["EFHK"] = AirportType.Civil });

        Assert.Equal(AirportType.Unclassified, service.GetByIcao("ZZZZ")!.Type);
    }

    [Fact]
    public async Task ApplyAirportTypes_IcaoLookupIsCaseInsensitive()
    {
        var service = new AirportDataService();
        await service.LoadAsync(_airportsCsvPath);

        service.ApplyAirportTypes(new Dictionary<string, AirportType> { ["efhk"] = AirportType.Private });

        Assert.Equal(AirportType.Private, service.GetByIcao("EFHK")!.Type);
    }

    [Fact]
    public void NewAirport_DefaultsToUnclassified()
    {
        var airport = new Airport();
        Assert.Equal(AirportType.Unclassified, airport.Type);
    }
}
