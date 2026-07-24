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

    // Map tab filter state, remembered across sessions so recurring searches
    // (e.g. "civil airports, 6000+ ft runway, in Europe") don't need to be re-entered
    // every launch. Saved whenever Apply or Clear is used; defaults match MapViewModel's.
    public int MinRunway { get; set; }
    public int MaxRunway { get; set; }
    public bool UseMeters { get; set; }
    public bool RequireInstrumentApproach { get; set; }
    public bool RequireAtis { get; set; }
    public string FilterCenterIcao { get; set; } = string.Empty;
    public double FilterRadiusNm { get; set; }
    public bool ShowVisited { get; set; } = true;
    public bool ShowNotVisited { get; set; } = true;
    public bool ShowCivilAirports { get; set; } = true;
    public bool ShowMilitaryAirports { get; set; } = true;
    public bool ShowHeliportAirports { get; set; } = true;
    public bool ShowPrivateAirports { get; set; } = true;
    public bool ShowOtherAirports { get; set; } = true;
    public bool ShowUnknownAirports { get; set; } = true;
    public bool ShowUnclassifiedAirports { get; set; } = true;
    public string IcaoPrefixes { get; set; } = string.Empty;
}
