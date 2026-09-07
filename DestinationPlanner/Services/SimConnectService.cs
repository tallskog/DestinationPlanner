using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
using System.Collections.Generic;
using Microsoft.FlightSimulator.SimConnect;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DestinationPlanner.Services;

// Requires Libs\Microsoft.FlightSimulator.SimConnect.dll (managed wrapper, x64)
// and Libs\SimConnect.dll (native, x64) — both copied from the MSFS SDK or a
// compatible tool installation. The native DLL must be next to the executable.
public class SimConnectService : ISimConnectService
{
    // WM_USER + 2  — arbitrary user-space message SimConnect posts to our window.
    private const int WM_USER_SIMCONNECT = 0x0402;

    private readonly IAirportDataService _airports;
    private readonly int _simDataRateHz;

    private SimConnect? _sc;
    private HwndSource? _hwndSource;
    private bool _initialized;   // true once we've received the first data frame
    private bool _lastBrake;     // last known parking-brake state
    private bool _lastOnGround = true;
    private bool _inFlight;      // true between block-off and block-on

    // MSFS briefly sends stale/loading-state data after a connection is established
    // (e.g. last-session coordinates before the actual flight position is loaded).
    // We absorb data for this many seconds, keeping _lastBrake/_lastOnGround current
    // but not firing any events, so the first real event comes from a stable state.
    private const int StabilizationSeconds = 5;
    private DateTime _connectedAt;

    private string?  _departureIcao;
    private DateTime _blockOffUtc;
    private string   _aircraftModel = string.Empty;

    // Captured at touchdown; carried into the FlightRecord when flight completes.
    private LandingStats? _lastLandingStats;

    // Last vertical speed observed while the aircraft was airborne.
    // Updated every frame when not on ground; used as the touchdown FPM at landing.
    // This gives the actual descent rate at contact, not the approach rate from seconds earlier.
    private double _lastAirborneVerticalSpeed;

    // Rolling window of G-force samples used to find the true impact peak.
    // G-force spikes briefly at the impact frame; the window ensures we don't miss it
    // if the GROUND state transition is detected one or two frames after actual contact.
    private readonly Queue<(double GForce, DateTime Time)> _landingWindow = new();
    private const int LandingWindowSeconds = 2;

    public bool IsConnected { get; private set; }
    public event EventHandler<FlightRecord>? FlightCompleted;
    public event EventHandler<FlightStartedEventArgs>? FlightStarted;
    public event EventHandler<bool>? OnGroundChanged;
    public event EventHandler? ConnectionChanged;
    public event EventHandler<AircraftPositionEventArgs>? PositionChanged;

    // SimConnect data-definition / request enums.
    private enum Defs { SimData }
    private enum Reqs { SimData }

    // Facility Data API — used to fetch the runway's *actual in-sim* position at
    // landing time (community scenery can reposition runways vs. OurAirports'
    // real-world survey coordinates; see LandingRatingHelper centerline scoring).
    private enum FacilityDefs { Runway }
    private enum FacilityReqs { Runway }

    // Unused beyond satisfying SimConnect's marshaling requirement: registering a
    // struct for the parent AIRPORT row is mandatory even when only RUNWAY fields
    // are requested, or the airport-row callback throws InvalidCastException.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FacilityAirportData
    {
        public double Latitude;
    }

    // One row per physical runway strip (both thresholds), not per direction.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FacilityRunwayData
    {
        public double Latitude, Longitude, Heading, Length;
        public int PrimaryNumber, PrimaryDesignator, SecondaryNumber, SecondaryDesignator;
    }

    // Holds a flight record that's fully built except for runway geometry, while we
    // wait for the async facility-data round trip (or its timeout/fallback) to resolve
    // the runway list to score it against.
    private sealed record PendingCompletion(
        FlightRecord Record, double Lat, double Lon, string ArrivalIcao,
        DateTime RequestSentUtc, List<Runway> LiveRunways);

    private PendingCompletion? _pending;
    private const int FacilityDataTimeoutSeconds = 3;

    // Field order must match AddToDataDefinition call order exactly (Pack=1, Sequential).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct SimData
    {
        public double Latitude;         // PLANE LATITUDE,           degrees
        public double Longitude;        // PLANE LONGITUDE,          degrees
        public double HeadingDegrees;   // PLANE HEADING DEGREES TRUE, degrees
        public double VerticalSpeed;    // VERTICAL SPEED,           feet/min (negative = descent)
        public double GForce;           // G FORCE,                  ratio (1.0 = 1g)
        public double AirspeedIndicated;// AIRSPEED INDICATED,       knots
        public double WindVelocity;     // AMBIENT WIND VELOCITY,    knots
        public double WindDirection;    // AMBIENT WIND DIRECTION,   degrees true
        public double BankDegrees;      // PLANE BANK DEGREES,       degrees (positive = right wing down)
        public double PitchDegrees;     // PLANE PITCH DEGREES,      degrees (positive = nose up)
        public int    ParkingBrake;     // BRAKE PARKING INDICATOR,  Bool (0/1)
        public int    OnGround;         // SIM ON GROUND,            Bool (0/1)
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;            // TITLE,                    aircraft name
    }

    private record LandingStats(
        double Fpm, double GForce, double AirspeedKts,
        double WindKts, double WindDirection,
        double Latitude, double Longitude,
        double HeadingDeg, double BankDeg, double PitchDeg);

    public SimConnectService(IAirportDataService airports, int simDataRateHz = 60)
    {
        _airports       = airports;
        _simDataRateHz  = simDataRateHz;
    }

    // Called from the UI thread (OnSourceInitialized / reconnect timer).
    public void Connect(nint windowHandle)
    {
        if (IsConnected) return;
        try
        {
            CleanupSimConnect();

            _sc = new SimConnect(
                "DestinationPlanner",
                (IntPtr)windowHandle,
                WM_USER_SIMCONNECT,
                null, 0);

            _sc.OnRecvOpen           += OnOpen;
            _sc.OnRecvQuit           += OnQuit;
            _sc.OnRecvException      += OnException;
            _sc.OnRecvSimobjectData  += OnSimobjectData;

            // Order must match SimData field order exactly.
            _sc.AddToDataDefinition(Defs.SimData, "PLANE LATITUDE",            "degrees",    SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "PLANE LONGITUDE",           "degrees",    SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "PLANE HEADING DEGREES TRUE","degrees",    SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "VERTICAL SPEED",            "feet/minute",SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "G FORCE",                   "gforce",     SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "AIRSPEED INDICATED",        "knots",      SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "AMBIENT WIND VELOCITY",     "knots",      SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "AMBIENT WIND DIRECTION",    "degrees",    SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "PLANE BANK DEGREES",        "degrees",    SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "PLANE PITCH DEGREES",       "degrees",    SIMCONNECT_DATATYPE.FLOAT64,   0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "BRAKE PARKING INDICATOR",   "Bool",       SIMCONNECT_DATATYPE.INT32,     0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "SIM ON GROUND",             "Bool",       SIMCONNECT_DATATYPE.INT32,     0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "TITLE",                     null,         SIMCONNECT_DATATYPE.STRING256, 0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.RegisterDataDefineStruct<SimData>(Defs.SimData);

            // Facility definition for live runway geometry at the arrival airport.
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "OPEN AIRPORT");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "LATITUDE");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "OPEN RUNWAY");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "LATITUDE");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "LONGITUDE");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "HEADING");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "LENGTH");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "PRIMARY_NUMBER");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "PRIMARY_DESIGNATOR");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "SECONDARY_NUMBER");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "SECONDARY_DESIGNATOR");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "CLOSE RUNWAY");
            _sc.AddToFacilityDefinition(FacilityDefs.Runway, "CLOSE AIRPORT");
            _sc.RegisterFacilityDataDefineStruct<FacilityAirportData>(SIMCONNECT_FACILITY_DATA_TYPE.AIRPORT);
            _sc.RegisterFacilityDataDefineStruct<FacilityRunwayData>(SIMCONNECT_FACILITY_DATA_TYPE.RUNWAY);
            _sc.OnRecvFacilityData    += OnFacilityData;
            _sc.OnRecvFacilityDataEnd += OnFacilityDataEnd;

            // Use VISUAL_FRAME for > 1 Hz to improve landing-stats accuracy.
            // interval = every Nth visual frame; at 60 fps sim: Hz = 60 / interval.
            var (period, interval) = _simDataRateHz > 1
                ? (SIMCONNECT_PERIOD.VISUAL_FRAME,
                   (uint)Math.Max(1, (int)Math.Round(60.0 / _simDataRateHz)))
                : (SIMCONNECT_PERIOD.SECOND, 0u);

            _sc.RequestDataOnSimObject(
                Reqs.SimData, Defs.SimData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                period,
                SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
                0, interval, 0);

            // Hook WndProc once — reuse the HwndSource across reconnects.
            if (_hwndSource is null)
            {
                _hwndSource = HwndSource.FromHwnd((IntPtr)windowHandle);
                _hwndSource?.AddHook(WndProc);
            }
        }
        catch (Exception ex) when (ex is COMException            // MSFS not running
                                    or FileNotFoundException        // native SimConnect.dll missing
                                    or BadImageFormatException      // architecture mismatch
                                    or TypeLoadException)           // assembly load failure
        {
            // SimConnect unavailable — stay disconnected, reconnect timer will retry.
            CleanupSimConnect();
        }
    }

    public void Disconnect()
    {
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        CleanupSimConnect();
        SetConnected(false);
    }

    // Dispatches SimConnect messages that arrive as window messages.
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_USER_SIMCONNECT)
        {
            _sc?.ReceiveMessage();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        _initialized      = false;
        _inFlight         = false;
        _lastOnGround     = true;
        _departureIcao    = null;
        _aircraftModel    = string.Empty;
        _lastLandingStats = null;
        _landingWindow.Clear();
        _pending          = null;
        _connectedAt      = DateTime.UtcNow;
        SetConnected(true);
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        // The sim is gone, so the live facility-data round trip will never complete —
        // finalize with the OurAirports fallback rather than losing an already-flown leg.
        if (_pending is not null)
            FinalizePending(_airports.GetByIcao(_pending.ArrivalIcao)?.Runways);

        _inFlight                   = false;
        _initialized                = false;
        _departureIcao              = null;
        _aircraftModel              = string.Empty;
        _lastLandingStats           = null;
        _lastAirborneVerticalSpeed  = 0;
        _landingWindow.Clear();
        CleanupSimConnect();
        SetConnected(false);
    }

    private void OnException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
    {
        // Non-fatal — log if needed; most are temporary data-unavailable errors.
    }

    private void OnSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID != (uint)Reqs.SimData) return;

        var sd       = (SimData)data.dwData[0];
        bool brakeOn  = sd.ParkingBrake != 0;
        bool onGround = sd.OnGround != 0;

        if (!_initialized)
        {
            // Establish initial state without triggering spurious events.
            _lastBrake    = brakeOn;
            _lastOnGround = onGround;
            _initialized  = true;
            return;
        }

        // Still inside the post-connect stabilization window: keep tracking state
        // but don't fire events. This absorbs any loading-state jitter from MSFS
        // (e.g. stale coordinates or brake transitions before the sim fully loads).
        if ((DateTime.UtcNow - _connectedAt).TotalSeconds < StabilizationSeconds)
        {
            _lastBrake    = brakeOn;
            _lastOnGround = onGround;
            return;
        }

        // A stalled/lost facility-data response shouldn't hold up logging the flight.
        if (_pending is not null && (DateTime.UtcNow - _pending.RequestSentUtc).TotalSeconds > FacilityDataTimeoutSeconds)
            FinalizePending(_airports.GetByIcao(_pending.ArrivalIcao)?.Runways);

        // Always broadcast position so the map marker stays current.
        PositionChanged?.Invoke(this, new AircraftPositionEventArgs(
            sd.Latitude, sd.Longitude, sd.HeadingDegrees));

        // Track last airborne vertical speed for accurate touchdown FPM.
        if (!onGround)
            _lastAirborneVerticalSpeed = sd.VerticalSpeed;

        // Maintain a short G-force window so the impact peak is captured even if
        // the GROUND state transitions one or two frames after actual contact.
        var now = DateTime.UtcNow;
        _landingWindow.Enqueue((sd.GForce, now));
        while (_landingWindow.Count > 0 &&
               (now - _landingWindow.Peek().Time).TotalSeconds > LandingWindowSeconds)
            _landingWindow.Dequeue();

        if (onGround != _lastOnGround)
        {
            // Capture landing stats at the moment of touchdown.
            if (onGround && _inFlight)
            {
                // FPM: use the last airborne reading — the actual descent rate at contact,
                // not a wider-window minimum which would reflect the approach phase.
                // GForce: scan the short window for the true impact peak.
                double peakGForce = sd.GForce;
                foreach (var (g, _) in _landingWindow)
                {
                    if (g > peakGForce) peakGForce = g;
                }
                _lastLandingStats = new LandingStats(
                    Fpm:          _lastAirborneVerticalSpeed,
                    GForce:       peakGForce,
                    AirspeedKts:  sd.AirspeedIndicated,
                    WindKts:      sd.WindVelocity,
                    WindDirection:sd.WindDirection,
                    Latitude:     sd.Latitude,
                    Longitude:    sd.Longitude,
                    HeadingDeg:   sd.HeadingDegrees,
                    BankDeg:      sd.BankDegrees,
                    PitchDeg:     -sd.PitchDegrees); // MSFS: negative = nose-up; negate to aviation convention
            }

            _lastOnGround = onGround;
            OnGroundChanged?.Invoke(this, onGround);
        }

        if (!_inFlight && _lastBrake && !brakeOn)
        {
            // Parking brake RELEASED.
            _inFlight = true;

            if (_departureIcao is null)
            {
                // First release — lock departure, block-off time, and aircraft for this flight.
                // Subsequent releases (e.g. after engine-start or taxi stops) reuse these.
                _departureIcao = NearestIcao(sd.Latitude, sd.Longitude);
                _blockOffUtc   = DateTime.UtcNow;
                _aircraftModel = sd.Title?.Trim() ?? string.Empty;
                FlightStarted?.Invoke(this, new FlightStartedEventArgs(
                    _departureIcao ?? "Unknown", _blockOffUtc, _aircraftModel));
            }
        }
        else if (_inFlight && !_lastBrake && brakeOn)
        {
            // Parking brake SET → potential block-on.
            var arrivalIcao = NearestIcao(sd.Latitude, sd.Longitude);
            var blockOnUtc  = DateTime.UtcNow;

            if (_departureIcao is not null && arrivalIcao is not null &&
                !string.Equals(_departureIcao, arrivalIcao, StringComparison.OrdinalIgnoreCase))
            {
                // Arrived at a different airport — flight complete, reset for the next one.
                var record = new FlightRecord
                {
                    Date                 = DateOnly.FromDateTime(DateTime.UtcNow),
                    AircraftModel        = _aircraftModel,
                    DepartureIcao        = _departureIcao,
                    ArrivalIcao          = arrivalIcao,
                    BlockOffUtc          = _blockOffUtc,
                    BlockOnUtc           = blockOnUtc,
                    LandingFpm           = _lastLandingStats?.Fpm,
                    LandingGForce        = _lastLandingStats?.GForce,
                    LandingAirspeedKts   = _lastLandingStats?.AirspeedKts,
                    LandingWindKts       = _lastLandingStats?.WindKts,
                    LandingWindDirection = _lastLandingStats?.WindDirection,
                    LandingHeadingDeg    = _lastLandingStats?.HeadingDeg,
                    LandingBankAngleDeg  = _lastLandingStats?.BankDeg,
                    LandingPitchAngleDeg = _lastLandingStats?.PitchDeg,
                };

                if (_lastLandingStats is not null)
                {
                    // Defer completion until the live in-sim runway geometry for this
                    // airport comes back (see OnFacilityData/OnFacilityDataEnd), so
                    // centerline/TDZ scoring reflects what's actually rendered here —
                    // not OurAirports' real-world survey coordinates — falling back to
                    // OurAirports only if the live lookup fails, is empty, or times out.
                    _pending = new PendingCompletion(
                        record, _lastLandingStats.Latitude, _lastLandingStats.Longitude,
                        arrivalIcao, DateTime.UtcNow, []);

                    try
                    {
                        _sc?.RequestFacilityData(FacilityDefs.Runway, FacilityReqs.Runway, arrivalIcao, "");
                    }
                    catch (COMException)
                    {
                        FinalizePending(_airports.GetByIcao(arrivalIcao)?.Runways);
                    }
                }
                else
                {
                    FlightCompleted?.Invoke(this, record);
                }

                _departureIcao    = null;
                _aircraftModel    = string.Empty;
                _lastLandingStats = null;
            }
            // Same airport: keep _departureIcao locked so the next release inherits it.

            _inFlight = false;
        }

        _lastBrake = brakeOn;
    }

    private void OnFacilityData(SimConnect sender, SIMCONNECT_RECV_FACILITY_DATA data)
    {
        if (_pending is null || data.UserRequestId != (uint)FacilityReqs.Runway) return;
        if ((SIMCONNECT_FACILITY_DATA_TYPE)data.Type != SIMCONNECT_FACILITY_DATA_TYPE.RUNWAY) return;

        var fr = (FacilityRunwayData)data.Data[0];
        _pending.LiveRunways.Add(ConvertFacilityRunway(fr));
    }

    private void OnFacilityDataEnd(SimConnect sender, SIMCONNECT_RECV_FACILITY_DATA_END data)
    {
        if (_pending is null || data.RequestId != (uint)FacilityReqs.Runway) return;

        var runways = _pending.LiveRunways.Count > 0
            ? (IReadOnlyList<Runway>)_pending.LiveRunways
            : _airports.GetByIcao(_pending.ArrivalIcao)?.Runways;
        FinalizePending(runways);
    }

    // Completes a flight record once its runway geometry source (live facility data,
    // the OurAirports fallback, or null) has been decided, and raises FlightCompleted.
    private void FinalizePending(IReadOnlyList<Runway>? runways)
    {
        var pending = _pending;
        if (pending is null) return;
        _pending = null;

        LandingRatingHelper.Enrich(pending.Record, pending.Lat, pending.Lon, runways);
        FlightCompleted?.Invoke(this, pending.Record);
    }

    // A facility RUNWAY record gives the strip's center point + the primary end's
    // approach heading + total length (meters), not threshold coordinates directly.
    // Derive the two thresholds by projecting +/- half-length along that heading,
    // using the same flat-earth approximation LandingRatingHelper's geometry uses.
    private static Runway ConvertFacilityRunway(FacilityRunwayData fr)
    {
        const double mPerLat = 110540;
        double mPerLon  = Math.Cos(fr.Latitude * Math.PI / 180) * 111320;
        double halfLenM = fr.Length / 2.0;
        double headingRad = fr.Heading * Math.PI / 180;
        double dLat = Math.Cos(headingRad) * halfLenM / mPerLat;
        double dLon = Math.Sin(headingRad) * halfLenM / mPerLon;

        return new Runway
        {
            Ident        = $"{DesignatorLabel(fr.PrimaryNumber, fr.PrimaryDesignator)}/{DesignatorLabel(fr.SecondaryNumber, fr.SecondaryDesignator)}",
            LengthFt     = (int)Math.Round(fr.Length * 3.28084),
            LeLatitude   = fr.Latitude - dLat,
            LeLongitude  = fr.Longitude - dLon,
            LeHeadingDeg = fr.Heading,
            HeLatitude   = fr.Latitude + dLat,
            HeLongitude  = fr.Longitude + dLon,
            HeHeadingDeg = (fr.Heading + 180) % 360,
        };
    }

    // Cosmetic label only (e.g. "09L") — never read by the centerline/TDZ geometry math,
    // which uses only the coordinates and heading above.
    private static string DesignatorLabel(int number, int designator)
    {
        string suffix = designator switch
        {
            1 => "L",
            2 => "R",
            3 => "C",
            _ => "",
        };
        return $"{number:00}{suffix}";
    }

    // Returns the ICAO of the nearest airport within 15 nm, or null if none found.
    // Only considers airports with standard ICAO-style identifiers (no hyphens).
    // OurAirports uses region codes like "US-12381" for airports without an ICAO
    // designation; those are not useful for a flight logbook.
    private string? NearestIcao(double lat, double lon)
    {
        if (!_airports.IsLoaded) return null;

        const double margin = 0.5; // ≈30 nm rough bounding box
        var candidates = _airports.GetInBounds(
            lat - margin, lat + margin,
            lon - margin, lon + margin);

        return candidates
            .Where(a => !a.Icao.Contains('-'))
            .Select(a => (a.Icao, dist: GeoHelper.DistanceNm(lat, lon, a.Latitude, a.Longitude)))
            .Where(x => x.dist <= 15.0)
            .OrderBy(x => x.dist)
            .Select(x => x.Icao)
            .FirstOrDefault();
    }

    private void CleanupSimConnect()
    {
        try { _sc?.Dispose(); } catch { }
        _sc = null;
    }

    private void SetConnected(bool value)
    {
        if (IsConnected == value) return;
        IsConnected = value;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }
}
