using DestinationPlanner.Helpers;
using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

// Airport filtering logic shared between the Map tab (MapViewModel) and AI-assisted trip
// planning (TripCandidateService, US41), extracted so both consumers use one tested filter
// engine instead of duplicating it.
public static class AirportFilterService
{
    // Narrows the full airport dataset to those within radiusNm of centerIcao.
    // Returns null if centerIcao isn't found in the loaded airport data.
    public static IEnumerable<Airport>? ApplyCenterRadius(IAirportDataService airports, string centerIcao, double radiusNm)
    {
        var center = airports.GetByIcao(centerIcao.Trim());
        if (center is null) return null;

        double degApprox = radiusNm / 60.0;
        return airports.GetInBounds(
            center.Latitude  - degApprox, center.Latitude  + degApprox,
            center.Longitude - degApprox, center.Longitude + degApprox)
            .Where(a => GeoHelper.DistanceNm(center.Latitude, center.Longitude, a.Latitude, a.Longitude) <= radiusNm);
    }

    // Narrows an already-given candidate set by center+radius distance — for callers whose
    // candidates didn't come from IAirportDataService.GetInBounds (e.g. logbook airports).
    // Returns null if centerIcao isn't found in the loaded airport data.
    public static IEnumerable<Airport>? ApplyCenterRadius(IEnumerable<Airport> candidates, IAirportDataService airports, string centerIcao, double radiusNm)
    {
        var center = airports.GetByIcao(centerIcao.Trim());
        if (center is null) return null;
        return candidates.Where(a => GeoHelper.DistanceNm(center.Latitude, center.Longitude, a.Latitude, a.Longitude) <= radiusNm);
    }

    // Runway length (min/max, already in feet), instrument-approach/ATIS requirement,
    // airport-type, and ICAO-prefix filters.
    public static IEnumerable<Airport> ApplyRunwayTypeAndPrefixFilters(IEnumerable<Airport> candidates, AirportFilterCriteria criteria)
    {
        if (criteria.RequireInstrumentApproach)
            candidates = candidates.Where(a => a.HasInstrumentApproach);

        if (criteria.RequireAtis)
            candidates = candidates.Where(a => a.HasAtis);

        if (criteria.MinRunwayFt > 0) candidates = candidates.Where(a => a.LongestRunwayFt >= criteria.MinRunwayFt);
        if (criteria.MaxRunwayFt > 0) candidates = candidates.Where(a => a.LongestRunwayFt <= criteria.MaxRunwayFt);

        candidates = ApplyAirportTypeFilter(candidates, criteria);

        if (criteria.IcaoPrefixes.Count > 0)
            candidates = candidates.Where(a => criteria.IcaoPrefixes.Any(p => a.Icao.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

        return candidates;
    }

    // All-checked (the default) is a no-op, so behavior is unchanged for anyone who has never
    // synced OpenAIP data (every airport is Unclassified).
    private static IEnumerable<Airport> ApplyAirportTypeFilter(IEnumerable<Airport> candidates, AirportFilterCriteria criteria)
    {
        if (criteria.ShowCivilAirports && criteria.ShowMilitaryAirports && criteria.ShowHeliportAirports
            && criteria.ShowPrivateAirports && criteria.ShowOtherAirports && criteria.ShowUnknownAirports
            && criteria.ShowUnclassifiedAirports)
            return candidates;

        return candidates.Where(a => a.Type switch
        {
            AirportType.Civil => criteria.ShowCivilAirports,
            AirportType.Military => criteria.ShowMilitaryAirports,
            AirportType.Heliport => criteria.ShowHeliportAirports,
            AirportType.Private => criteria.ShowPrivateAirports,
            AirportType.Other => criteria.ShowOtherAirports,
            AirportType.Unknown => criteria.ShowUnknownAirports,
            _ => criteria.ShowUnclassifiedAirports, // Unclassified
        });
    }
}
