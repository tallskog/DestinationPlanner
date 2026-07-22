using DestinationPlanner.Models;
using DestinationPlanner.Services;
using Microsoft.Data.Sqlite;
using System.IO;

namespace DestinationPlanner.Tests.Services;

public class NavigraphDataServiceTests : IDisposable
{
    private readonly string _sqlitePath = Path.Combine(Path.GetTempPath(), $"dfd-{Guid.NewGuid():N}.3sdb");

    public NavigraphDataServiceTests()
    {
        using var connection = new SqliteConnection($"Data Source={_sqlitePath}");
        connection.Open();

        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE tbl_pa_airports (icao_code TEXT, airport_type TEXT)";
        create.ExecuteNonQuery();

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO tbl_pa_airports (icao_code, airport_type) VALUES
                ('EFHK', 'C'),
                ('EFTU', 'M'),
                ('EGLL', 'P'),
                ('ZZZZ', 'X'),
                ('YYYY', NULL)
            """;
        insert.ExecuteNonQuery();
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections by default, which keeps a file handle
        // open in the background even after the `using` block in ParseAirportTypes exits.
        SqliteConnection.ClearAllPools();
        File.Delete(_sqlitePath);
    }

    [Fact]
    public void ParseAirportTypes_MapsArincCodesCorrectly()
    {
        var service = new NavigraphDataService();
        var result = service.ParseAirportTypes(_sqlitePath);

        Assert.Equal(AirportType.Civil, result["EFHK"]);
        Assert.Equal(AirportType.Military, result["EFTU"]);
        Assert.Equal(AirportType.Private, result["EGLL"]);
    }

    [Fact]
    public void ParseAirportTypes_UnrecognizedOrNullCodeBecomesUnknown_NeverThrows()
    {
        var service = new NavigraphDataService();
        var result = service.ParseAirportTypes(_sqlitePath);

        Assert.Equal(AirportType.Unknown, result["ZZZZ"]);
        Assert.Equal(AirportType.Unknown, result["YYYY"]);
    }

    [Fact]
    public void ParseAirportTypes_IcaoKeyLookupIsCaseInsensitive()
    {
        var service = new NavigraphDataService();
        var result = service.ParseAirportTypes(_sqlitePath);

        Assert.True(result.ContainsKey("efhk"));
        Assert.Equal(AirportType.Civil, result["efhk"]);
    }
}
