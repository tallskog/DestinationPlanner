using DestinationPlanner.Serialization;
using System.IO;

namespace DestinationPlanner.Tests.Serialization;

public class ForeignLogbookImporterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"foreign-{Guid.NewGuid():N}.xml");

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Import_ValidRecord_ParsesFieldsAndPreservesLocalDate()
    {
        File.WriteAllText(_path, """
            <ArrayOfFlightRecord>
              <FlightRecord>
                <From>EGLL</From>
                <To>EGCC</To>
                <Date>23.3.2026</Date>
                <DepartureTime>16.53</DepartureTime>
                <ArrivalTime>18.09</ArrivalTime>
                <Aircraft>Airbus A320</Aircraft>
              </FlightRecord>
            </ArrayOfFlightRecord>
            """);

        var flight = ForeignLogbookImporter.Import(_path).Single();

        Assert.Equal("EGLL", flight.DepartureIcao);
        Assert.Equal("EGCC", flight.ArrivalIcao);
        Assert.Equal("Airbus A320", flight.AircraftModel);
        Assert.Equal(new DateOnly(2026, 3, 23), flight.Date);
        Assert.Equal(DateTimeKind.Utc, flight.BlockOffUtc.Kind);
        // Duration survives the local->UTC conversion regardless of the test machine's timezone.
        Assert.Equal(TimeSpan.FromMinutes(76), flight.BlockOnUtc - flight.BlockOffUtc);
    }

    [Fact]
    public void Import_SameDepartureAndArrival_IsSkipped()
    {
        File.WriteAllText(_path, """
            <ArrayOfFlightRecord>
              <FlightRecord>
                <From>EGLL</From>
                <To>EGLL</To>
                <Date>23.3.2026</Date>
                <DepartureTime>16.53</DepartureTime>
                <ArrivalTime>18.09</ArrivalTime>
              </FlightRecord>
            </ArrayOfFlightRecord>
            """);

        Assert.Empty(ForeignLogbookImporter.Import(_path));
    }

    [Fact]
    public void Import_MissingFromOrTo_IsSkipped()
    {
        File.WriteAllText(_path, """
            <ArrayOfFlightRecord>
              <FlightRecord>
                <From></From>
                <To>EGCC</To>
                <Date>23.3.2026</Date>
                <DepartureTime>16.53</DepartureTime>
                <ArrivalTime>18.09</ArrivalTime>
              </FlightRecord>
            </ArrayOfFlightRecord>
            """);

        Assert.Empty(ForeignLogbookImporter.Import(_path));
    }

    [Fact]
    public void Import_ArrivalBeforeDepartureTime_RollsOverToNextDayForDuration()
    {
        File.WriteAllText(_path, """
            <ArrayOfFlightRecord>
              <FlightRecord>
                <From>EFHK</From>
                <To>EFTU</To>
                <Date>23.3.2026</Date>
                <DepartureTime>23.50</DepartureTime>
                <ArrivalTime>0.15</ArrivalTime>
              </FlightRecord>
            </ArrayOfFlightRecord>
            """);

        var flight = ForeignLogbookImporter.Import(_path).Single();

        Assert.Equal(new DateOnly(2026, 3, 23), flight.Date); // Date field itself is not rolled over
        Assert.Equal(TimeSpan.FromMinutes(25), flight.BlockOnUtc - flight.BlockOffUtc);
    }

    [Fact]
    public void Import_InvalidDateFormat_IsSkipped()
    {
        File.WriteAllText(_path, """
            <ArrayOfFlightRecord>
              <FlightRecord>
                <From>EGLL</From>
                <To>EGCC</To>
                <Date>2026-03-23</Date>
                <DepartureTime>16.53</DepartureTime>
                <ArrivalTime>18.09</ArrivalTime>
              </FlightRecord>
            </ArrayOfFlightRecord>
            """);

        Assert.Empty(ForeignLogbookImporter.Import(_path));
    }
}
