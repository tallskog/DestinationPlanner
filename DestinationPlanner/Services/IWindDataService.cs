using DestinationPlanner.Models;

namespace DestinationPlanner.Services;

public interface IWindDataService
{
    // Fetches wind speed/direction at the given pressure level for a batch of points in a
    // single request. On success, Samples holds as many points as could be parsed (points
    // that individually fail to parse are skipped, not fatal — Samples may legitimately be
    // empty if none of them had data). Failure is non-null only when the request itself
    // failed entirely, and says why (see WindFetchFailure).
    Task<WindFetchResult> GetWindGridAsync(
        IReadOnlyList<(double Lat, double Lon)> points,
        int pressureHPa,
        CancellationToken cancellationToken = default);
}
