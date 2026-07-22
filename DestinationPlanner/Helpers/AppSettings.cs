namespace DestinationPlanner.Helpers;

public class AppSettings
{
    // Approximate SimConnect data sampling rate in Hz.
    // Values > 1 use VISUAL_FRAME sampling (actual rate = sim framerate / interval).
    // 60 = every visual frame at 60 fps; 10 = every 6th frame at 60 fps.
    public int SimDataRateHz { get; set; } = 60;

    // Path of the logbook that was active in the previous session.
    // Null means no session has been saved yet (first-run or cleared).
    public string? LastLogbookPath { get; set; }

    // DPAPI-protected (CurrentUser scope), Base64-encoded Navigraph OAuth refresh token.
    // Null means never signed in, or a previous refresh/decrypt attempt failed and the
    // user must sign in again. See NavigraphTokenStore. Unlike the other settings above,
    // this is a real credential, so it's encrypted at rest rather than stored as plain text.
    public string? NavigraphRefreshTokenProtected { get; set; }
}
