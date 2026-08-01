using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public sealed class TripCandidateService(IAirportDataService airportData, ILogbookService logbook) : ITripCandidateService
{
    public IReadOnlyList<Airport> GetCandidates(AirportFilterCriteria criteria, bool excludeVisited)
    {
        if (!airportData.IsLoaded) return [];

        IEnumerable<Airport>? pool;
        if (!string.IsNullOrWhiteSpace(criteria.FilterCenterIcao) && criteria.FilterRadiusNm > 0)
        {
            pool = AirportFilterService.ApplyCenterRadius(airportData, criteria.FilterCenterIcao, criteria.FilterRadiusNm);
            if (pool is null) return [];
        }
        else
        {
            pool = airportData.GetAll();
        }

        var filtered = AirportFilterService.ApplyRunwayTypeAndPrefixFilters(pool, criteria);

        if (excludeVisited)
        {
            var visited = logbook.Flights
                .SelectMany(f => new[] { f.DepartureIcao, f.ArrivalIcao })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            filtered = filtered.Where(a => !visited.Contains(a.Icao));
        }

        var overrides = AirportFilterService.ResolveIcaoOverrides(airportData, criteria.IcaoPrefixes);
        return filtered.UnionBy(overrides, a => a.Icao, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
