using DestinationPlanner.Helpers;
using DestinationPlanner.Models;
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

    public bool IsConnected { get; private set; }
    public event EventHandler<FlightRecord>? FlightCompleted;
    public event EventHandler<FlightStartedEventArgs>? FlightStarted;
    public event EventHandler<bool>? OnGroundChanged;
    public event EventHandler? ConnectionChanged;
    public event EventHandler<AircraftPositionEventArgs>? PositionChanged;

    // SimConnect data-definition / request enums.
    private enum Defs { SimData }
    private enum Reqs { SimData }

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
        public int    ParkingBrake;     // BRAKE PARKING INDICATOR,  Bool (0/1)
        public int    OnGround;         // SIM ON GROUND,            Bool (0/1)
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Title;            // TITLE,                    aircraft name
    }

    private record LandingStats(
        double Fpm, double GForce, double AirspeedKts,
        double WindKts, double WindDirection);

    public SimConnectService(IAirportDataService airports) => _airports = airports;

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
            _sc.AddToDataDefinition(Defs.SimData, "BRAKE PARKING INDICATOR",   "Bool",       SIMCONNECT_DATATYPE.INT32,     0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "SIM ON GROUND",             "Bool",       SIMCONNECT_DATATYPE.INT32,     0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "TITLE",                     null,         SIMCONNECT_DATATYPE.STRING256, 0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.RegisterDataDefineStruct<SimData>(Defs.SimData);

            _sc.RequestDataOnSimObject(
                Reqs.SimData, Defs.SimData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.CHANGED,
                0, 0, 0);

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
        _connectedAt      = DateTime.UtcNow;
        SetConnected(true);
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        _inFlight         = false;
        _initialized      = false;
        _departureIcao    = null;
        _aircraftModel    = string.Empty;
        _lastLandingStats = null;
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

        // Always broadcast position so the map marker stays current.
        PositionChanged?.Invoke(this, new AircraftPositionEventArgs(
            sd.Latitude, sd.Longitude, sd.HeadingDegrees));

        if (onGround != _lastOnGround)
        {
            // Capture landing stats at the moment of touchdown.
            if (onGround && _inFlight)
            {
                _lastLandingStats = new LandingStats(
                    Fpm:          sd.VerticalSpeed,
                    GForce:       sd.GForce,
                    AirspeedKts:  sd.AirspeedIndicated,
                    WindKts:      sd.WindVelocity,
                    WindDirection:sd.WindDirection);
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
                FlightCompleted?.Invoke(this, new FlightRecord
                {
                    Date                = DateOnly.FromDateTime(DateTime.UtcNow),
                    AircraftModel       = _aircraftModel,
                    DepartureIcao       = _departureIcao,
                    ArrivalIcao         = arrivalIcao,
                    BlockOffUtc         = _blockOffUtc,
                    BlockOnUtc          = blockOnUtc,
                    LandingFpm          = _lastLandingStats?.Fpm,
                    LandingGForce       = _lastLandingStats?.GForce,
                    LandingAirspeedKts  = _lastLandingStats?.AirspeedKts,
                    LandingWindKts      = _lastLandingStats?.WindKts,
                    LandingWindDirection= _lastLandingStats?.WindDirection,
                });
                _departureIcao    = null;
                _aircraftModel    = string.Empty;
                _lastLandingStats = null;
            }
            // Same airport: keep _departureIcao locked so the next release inherits it.

            _inFlight = false;
        }

        _lastBrake = brakeOn;
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
