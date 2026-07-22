using DestinationPlanner.Serialization;
using System.IO;

namespace DestinationPlanner.Tests.Serialization;

public class LittleNavmapCsvImporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lnm-{Guid.NewGuid():N}.csv");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Import_ValidCsv_ParsesFieldsWithOffsetAwareUtcConversion()
    {
        File.WriteAllText(_path,
            "Departure Ident,Destination Ident,Departure Time,Destination Time,Aircraft Name,Aircraft Type\n" +
            "EGLL,EGCC,2025-01-15T17:42:35.082+02:00,2025-01-15T18:58:00.000+02:00,Airbus A320,A20N\n");

        var flight = LittleNavmapCsvImporter.Import(_path).Single();

        Assert.Equal("EGLL", flight.DepartureIcao);
        Assert.Equal("EGCC", flight.ArrivalIcao);
        Assert.Equal(new DateTime(2025, 1, 15, 15, 42, 35, 82, DateTimeKind.Utc), flight.BlockOffUtc);
        Assert.Equal(new DateTime(2025, 1, 15, 16, 58, 0, DateTimeKind.Utc), flight.BlockOnUtc);
        Assert.Equal("Airbus A320 A20N", flight.AircraftModel);
    }

    [Fact]
    public void Import_CoordinateStyleIdent_IsSkippedAsWaypoint()
    {
        File.WriteAllText(_path,
            "Departure Ident,Destination Ident,Departure Time,Destination Time\n" +
            "4951N00835E,EGCC,2025-01-15T17:42:35+02:00,2025-01-15T18:58:00+02:00\n");

        Assert.Empty(LittleNavmapCsvImporter.Import(_path));
    }

    [Fact]
    public void Import_SameDepartureAndArrival_IsSkipped()
    {
        File.WriteAllText(_path,
            "Departure Ident,Destination Ident,Departure Time,Destination Time\n" +
            "EGLL,EGLL,2025-01-15T17:42:35+02:00,2025-01-15T18:58:00+02:00\n");

        Assert.Empty(LittleNavmapCsvImporter.Import(_path));
    }

    [Fact]
    public void Import_MissingRequiredColumns_ThrowsNotSupportedException()
    {
        File.WriteAllText(_path, "Some,Other,Columns\nA,B,C\n");

        Assert.Throws<NotSupportedException>(() => LittleNavmapCsvImporter.Import(_path).ToList());
    }

    [Fact]
    public void Import_QuotedFieldWithEmbeddedComma_IsParsedAsOneField()
    {
        File.WriteAllText(_path,
            "Departure Ident,Destination Ident,Departure Time,Destination Time,Aircraft Name,Aircraft Type\n" +
            "EGLL,EGCC,2025-01-15T17:42:35+02:00,2025-01-15T18:58:00+02:00,\"Airbus, A320\",A20N\n");

        var flight = LittleNavmapCsvImporter.Import(_path).Single();

        Assert.Equal("Airbus, A320 A20N", flight.AircraftModel);
    }

    [Fact]
    public void Import_DollarPrefixedAircraftField_StripsPrefix()
    {
        File.WriteAllText(_path,
            "Departure Ident,Destination Ident,Departure Time,Destination Time,Aircraft Name,Aircraft Type\n" +
            "EGLL,EGCC,2025-01-15T17:42:35+02:00,2025-01-15T18:58:00+02:00,$$:Cessna 172,\n");

        var flight = LittleNavmapCsvImporter.Import(_path).Single();

        Assert.Equal("Cessna 172", flight.AircraftModel);
    }

    [Fact]
    public void Import_RawAtcAircraftName_IsDiscardedFromModel()
    {
        File.WriteAllText(_path,
            "Departure Ident,Destination Ident,Departure Time,Destination Time,Aircraft Name,Aircraft Type\n" +
            "EGLL,EGCC,2025-01-15T17:42:35+02:00,2025-01-15T18:58:00+02:00,ATCCOM_AC_A320,A20N\n");

        var flight = LittleNavmapCsvImporter.Import(_path).Single();

        Assert.Equal("A20N", flight.AircraftModel);
    }
}
