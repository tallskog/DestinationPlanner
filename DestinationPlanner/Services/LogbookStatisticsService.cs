using DestinationPlanner.Helpers;
using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public static class LogbookStatisticsService
{
    public static LogbookStatistics Calculate(IReadOnlyList<FlightRecord> flights, IAirportDataService airportData)
    {
        if (flights.Count == 0)
        {
            return new LogbookStatistics(
                TotalFlights: 0,
                VisitedAirportCount: 0,
                TopVisitedAirports: [],
                TopLandedAirports: [],
                TopDepartedAirports: [],
                TopRoutes: [],
                LongestLegByTime: null,
                LongestLegByDistance: null,
                AverageLegTime: null,
                AverageLegDistanceNm: null,
                TotalTime: TimeSpan.Zero,
                TotalDistanceNm: null,
                FirstFlightDate: null,
                LastFlightDate: null,
                AircraftTypes: []);
        }

        var visited = flights
            .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var topVisited = flights
            .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
            .GroupBy(icao => icao, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new AirportCountStat(g.Key, g.Count()))
            .ToList();

        var topLanded = flights
            .GroupBy(f => f.ArrivalIcao, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new AirportCountStat(g.Key, g.Count()))
            .ToList();

        var topDeparted = flights
            .GroupBy(f => f.DepartureIcao, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new AirportCountStat(g.Key, g.Count()))
            .ToList();

        var topRoutes = flights
            .GroupBy(f => (f.DepartureIcao, f.ArrivalIcao), new RouteComparer())
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new RouteCountStat(g.Key.DepartureIcao, g.Key.ArrivalIcao, g.Count()))
            .ToList();

        var longestByTime = flights.MaxBy(f => f.BlockTime);
        var totalTime = TimeSpan.FromTicks(flights.Sum(f => f.BlockTime.Ticks));
        var averageTime = TimeSpan.FromTicks(totalTime.Ticks / flights.Count);

        var legsWithDistance = new List<(FlightRecord Flight, double DistanceNm)>();
        foreach (var f in flights)
        {
            var dep = airportData.GetByIcao(f.DepartureIcao);
            var arr = airportData.GetByIcao(f.ArrivalIcao);
            if (dep is null || arr is null) continue;
            var distanceNm = GeoHelper.DistanceNm(dep.Latitude, dep.Longitude, arr.Latitude, arr.Longitude);
            legsWithDistance.Add((f, distanceNm));
        }

        (FlightRecord, double)? longestByDistance = legsWithDistance.Count > 0
            ? legsWithDistance.MaxBy(l => l.DistanceNm)
            : null;
        double? averageDistance = legsWithDistance.Count > 0
            ? legsWithDistance.Average(l => l.DistanceNm)
            : null;
        double? totalDistance = legsWithDistance.Count > 0
            ? legsWithDistance.Sum(l => l.DistanceNm)
            : null;

        var aircraftTypes = flights
            .GroupBy(f => f.AircraftModel)
            .Select(g => new AircraftTypeStat(g.Key, g.Count(), TimeSpan.FromTicks(g.Sum(f => f.BlockTime.Ticks))))
            .OrderByDescending(a => a.TotalTime)
            .ToList();

        return new LogbookStatistics(
            TotalFlights: flights.Count,
            VisitedAirportCount: visited.Count,
            TopVisitedAirports: topVisited,
            TopLandedAirports: topLanded,
            TopDepartedAirports: topDeparted,
            TopRoutes: topRoutes,
            LongestLegByTime: longestByTime,
            LongestLegByDistance: longestByDistance,
            AverageLegTime: averageTime,
            AverageLegDistanceNm: averageDistance,
            TotalTime: totalTime,
            TotalDistanceNm: totalDistance,
            FirstFlightDate: flights.Min(f => f.Date),
            LastFlightDate: flights.Max(f => f.Date),
            AircraftTypes: aircraftTypes);
    }

    private sealed class RouteComparer : IEqualityComparer<(string DepartureIcao, string ArrivalIcao)>
    {
        public bool Equals((string DepartureIcao, string ArrivalIcao) x, (string DepartureIcao, string ArrivalIcao) y) =>
            string.Equals(x.DepartureIcao, y.DepartureIcao, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ArrivalIcao, y.ArrivalIcao, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string DepartureIcao, string ArrivalIcao) obj) =>
            HashCode.Combine(
                obj.DepartureIcao.ToUpperInvariant(),
                obj.ArrivalIcao.ToUpperInvariant());
    }
}
