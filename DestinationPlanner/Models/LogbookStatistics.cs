namespace DestinationPlanner.Models;

public sealed record LogbookStatistics(
    int TotalFlights,
    int VisitedAirportCount,
    IReadOnlyList<AirportCountStat> TopVisitedAirports,
    IReadOnlyList<AirportCountStat> TopLandedAirports,
    IReadOnlyList<AirportCountStat> TopDepartedAirports,
    IReadOnlyList<RouteCountStat> TopRoutes,
    FlightRecord? LongestLegByTime,
    (FlightRecord Flight, double DistanceNm)? LongestLegByDistance,
    TimeSpan? AverageLegTime,
    double? AverageLegDistanceNm,
    TimeSpan TotalTime,
    double? TotalDistanceNm,
    DateOnly? FirstFlightDate,
    DateOnly? LastFlightDate,
    IReadOnlyList<AircraftTypeStat> AircraftTypes);

public sealed record AirportCountStat(string Icao, int Count);

public sealed record RouteCountStat(string DepartureIcao, string ArrivalIcao, int Count);

public sealed record AircraftTypeStat(string Model, int LegCount, TimeSpan TotalTime);
