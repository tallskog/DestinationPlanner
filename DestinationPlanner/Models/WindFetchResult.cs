namespace DestinationPlanner.Models;

// Why a wind-grid fetch didn't return usable samples, distinguished so the UI can show
// something more specific than a generic "unavailable" — see BUG-09/BUG-11 in requirements.md.
public enum WindFetchFailure
{
    RateLimited,
    ServiceUnavailable,
    NetworkError,
}

public sealed record WindFetchResult(IReadOnlyList<WindSample> Samples, WindFetchFailure? Failure)
{
    public static WindFetchResult Success(IReadOnlyList<WindSample> samples) => new(samples, null);
    public static WindFetchResult Failed(WindFetchFailure failure) => new(Array.Empty<WindSample>(), failure);
}
