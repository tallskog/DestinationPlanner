namespace DestinationPlanner.Services;

// A single precipitation radar frame (rain, snow, sleet, or hail): a Mapsui/BruTile-compatible
// XYZ tile URL template (containing literal "{z}/{x}/{y}" tokens) and the UTC time the frame
// was observed.
public record PrecipitationRadarFrame(string TileUrlTemplate, DateTimeOffset FrameTimeUtc);

public interface IPrecipitationRadarService
{
    // Returns the most recently observed radar frame, or null if the fetch/parse fails
    // (network error, malformed response, or missing fields). Never throws.
    Task<PrecipitationRadarFrame?> GetLatestFrameAsync(CancellationToken cancellationToken = default);
}
