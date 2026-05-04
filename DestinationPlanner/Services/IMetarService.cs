namespace DestinationPlanner.Services;

public interface IMetarService
{
    Task<string?> FetchMetarAsync(string icao, CancellationToken cancellationToken = default);
}
