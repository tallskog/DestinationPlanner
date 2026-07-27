using DestinationPlanner.Helpers;
using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// Deterministic leg sequencing under a per-leg distance constraint (US41 follow-up).
//
// Asking Claude to respect a numeric window like "each leg between 150 and 300 nm" from bare
// ICAO codes means trusting its memorized geography to estimate distances it can't actually
// compute — exactly the kind of data invention this app avoids everywhere else (see
// RegionLookup). Real lat/lon is already available via IAirportDataService, so when a distance
// constraint is present the route is built here instead, using GeoHelper.DistanceNm — every
// leg this produces is guaranteed to satisfy the constraint. The AI's role then narrows to
// writing a narrative for the fixed leg list (IAiTripPlanningService.NarrateAsync).
public static class TripRouteBuilder
{
    // Greedy nearest-neighbor walk: from the current airport, always hop to the nearest
    // unvisited candidate whose distance falls within [minNm, maxNm]. Stops (rather than
    // failing) once no unvisited candidate is in range — not every candidate is guaranteed
    // a place in the route, which is the honest outcome of a real geometric constraint.
    public static IReadOnlyList<(Airport From, Airport To, double DistanceNm)> BuildRoute(
        IReadOnlyList<Airport> candidates, double minNm, double maxNm, string? startIcao)
    {
        var route = new List<(Airport From, Airport To, double DistanceNm)>();
        if (candidates.Count < 2) return route;

        var remaining = candidates.ToList();
        var current = (string.IsNullOrWhiteSpace(startIcao)
            ? null
            : remaining.FirstOrDefault(a => a.Icao.Equals(startIcao, StringComparison.OrdinalIgnoreCase)))
            ?? remaining[0];
        remaining.Remove(current);

        while (remaining.Count > 0)
        {
            Airport? next = null;
            double bestDistance = double.MaxValue;

            foreach (var candidate in remaining)
            {
                double distance = GeoHelper.DistanceNm(
                    current.Latitude, current.Longitude, candidate.Latitude, candidate.Longitude);

                if (distance >= minNm && distance <= maxNm && distance < bestDistance)
                {
                    next = candidate;
                    bestDistance = distance;
                }
            }

            if (next is null) break; // no remaining candidate is within range of the current airport

            route.Add((current, next, bestDistance));
            remaining.Remove(next);
            current = next;
        }

        return route;
    }
}
