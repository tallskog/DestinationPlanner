using DestinationPlanner.Models;
using DestinationPlanner.Serialization;
using System.IO;

namespace DestinationPlanner.Tests.Serialization;

public class NativeLogbookSerializerTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"logbook-{Guid.NewGuid():N}.xml");

    public void Dispose() => File.Delete(_path);

    private static FlightRecord SampleFlight() => new()
    {
        Date = new DateOnly(2026, 4, 26),
        AircraftModel = "Airbus A320",
        DepartureIcao = "EGLL",
        ArrivalIcao = "EGCC",
        BlockOffUtc = new DateTime(2026, 4, 26, 10, 30, 0, DateTimeKind.Utc),
        BlockOnUtc = new DateTime(2026, 4, 26, 11, 45, 0, DateTimeKind.Utc),
        LandingFpm = -180,
        LandingGForce = 1.2,
        LandingAirspeedKts = 134,
        LandingWindKts = 12,
        LandingWindDirection = 270,
        LandingHeadingDeg = 270,
        LandingBankAngleDeg = 3.5,
        LandingPitchAngleDeg = 4.1,
        LandingCrosswindKts = 6.0,
        LandingCenterlineDeviationFt = 8.0,
        LandingTouchdownZonePct = 22.0,
        LandingStars = 4,
    };

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var original = SampleFlight();

        NativeLogbookSerializer.Save([original], _path);
        var loaded = NativeLogbookSerializer.Load(_path).Single();

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.Date, loaded.Date);
        Assert.Equal(original.AircraftModel, loaded.AircraftModel);
        Assert.Equal(original.DepartureIcao, loaded.DepartureIcao);
        Assert.Equal(original.ArrivalIcao, loaded.ArrivalIcao);
        Assert.Equal(original.BlockOffUtc, loaded.BlockOffUtc);
        Assert.Equal(original.BlockOnUtc, loaded.BlockOnUtc);
        Assert.Equal(original.LandingFpm!.Value, loaded.LandingFpm!.Value, precision: 1);
        Assert.Equal(original.LandingGForce!.Value, loaded.LandingGForce!.Value, precision: 2);
        Assert.Equal(original.LandingStars, loaded.LandingStars);
    }

    [Fact]
    public void SaveThenLoad_NullLandingFields_OmittedAndLoadAsNull()
    {
        var original = new FlightRecord
        {
            Date = new DateOnly(2026, 1, 1),
            DepartureIcao = "EFHK",
            ArrivalIcao = "EFTU",
            BlockOffUtc = DateTime.UtcNow,
            BlockOnUtc = DateTime.UtcNow.AddHours(1),
        };

        NativeLogbookSerializer.Save([original], _path);
        var loaded = NativeLogbookSerializer.Load(_path).Single();

        Assert.Null(loaded.LandingFpm);
        Assert.Null(loaded.LandingStars);
        Assert.Null(loaded.LandingCrosswindKts);
    }

    [Fact]
    public void Load_OlderFileWithoutNewLandingElements_LoadsWithNullNewFields()
    {
        // Simulates a pre-v1.2 logbook file: no LandingBankAngleDeg/PitchAngleDeg/etc elements at all.
        File.WriteAllText(_path, """
            <?xml version="1.0" encoding="utf-8"?>
            <FlightLogbook xmlns="urn:destination-planner:logbook:v1" version="1.0">
              <Flights>
                <Flight>
                  <Id>3fa85f64-5717-4562-b3fc-2c963f66afa6</Id>
                  <Date>2026-04-26</Date>
                  <AircraftModel>Airbus A320</AircraftModel>
                  <DepartureIcao>EGLL</DepartureIcao>
                  <ArrivalIcao>EGCC</ArrivalIcao>
                  <BlockOffUtc>2026-04-26T10:30:00Z</BlockOffUtc>
                  <BlockOnUtc>2026-04-26T11:45:00Z</BlockOnUtc>
                </Flight>
              </Flights>
            </FlightLogbook>
            """);

        var loaded = NativeLogbookSerializer.Load(_path).Single();

        Assert.Equal("EGLL", loaded.DepartureIcao);
        Assert.Null(loaded.LandingBankAngleDeg);
        Assert.Null(loaded.LandingStars);
    }

    [Fact]
    public void Load_UnknownLegacyElement_IsSilentlyIgnored()
    {
        // Simulates a file written by an older version that still had an AircraftType element.
        File.WriteAllText(_path, """
            <?xml version="1.0" encoding="utf-8"?>
            <FlightLogbook xmlns="urn:destination-planner:logbook:v1" version="1.0">
              <Flights>
                <Flight>
                  <Id>3fa85f64-5717-4562-b3fc-2c963f66afa6</Id>
                  <Date>2026-04-26</Date>
                  <AircraftType>Airplane</AircraftType>
                  <AircraftModel>Airbus A320</AircraftModel>
                  <DepartureIcao>EGLL</DepartureIcao>
                  <ArrivalIcao>EGCC</ArrivalIcao>
                  <BlockOffUtc>2026-04-26T10:30:00Z</BlockOffUtc>
                  <BlockOnUtc>2026-04-26T11:45:00Z</BlockOnUtc>
                </Flight>
              </Flights>
            </FlightLogbook>
            """);

        var loaded = NativeLogbookSerializer.Load(_path).Single();

        Assert.Equal("EGLL", loaded.DepartureIcao);
    }

    [Fact]
    public void Load_OneMalformedRecordAmongValidOnes_SkipsOnlyTheMalformedOne()
    {
        File.WriteAllText(_path, """
            <?xml version="1.0" encoding="utf-8"?>
            <FlightLogbook xmlns="urn:destination-planner:logbook:v1" version="1.0">
              <Flights>
                <Flight>
                  <Id>3fa85f64-5717-4562-b3fc-2c963f66afa6</Id>
                  <Date>2026-04-26</Date>
                  <AircraftModel>Airbus A320</AircraftModel>
                  <DepartureIcao>EGLL</DepartureIcao>
                  <ArrivalIcao>EGCC</ArrivalIcao>
                  <BlockOffUtc>2026-04-26T10:30:00Z</BlockOffUtc>
                  <BlockOnUtc>2026-04-26T11:45:00Z</BlockOnUtc>
                </Flight>
                <Flight>
                  <!-- Missing required Id element -->
                  <Date>2026-04-27</Date>
                  <DepartureIcao>EFHK</DepartureIcao>
                  <ArrivalIcao>EFTU</ArrivalIcao>
                  <BlockOffUtc>2026-04-27T10:30:00Z</BlockOffUtc>
                  <BlockOnUtc>2026-04-27T11:45:00Z</BlockOnUtc>
                </Flight>
              </Flights>
            </FlightLogbook>
            """);

        var loaded = NativeLogbookSerializer.Load(_path).ToList();

        Assert.Single(loaded);
        Assert.Equal("EGLL", loaded[0].DepartureIcao);
    }
}
