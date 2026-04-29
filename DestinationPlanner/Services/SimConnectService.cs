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
    private bool _inFlight;      // true between block-off and block-on

    private string?  _departureIcao;
    private DateTime _blockOffUtc;

    public bool IsConnected { get; private set; }
    public event EventHandler<FlightRecord>? FlightCompleted;
    public event EventHandler? ConnectionChanged;

    // SimConnect data-definition / request enums.
    private enum Defs { SimData }
    private enum Reqs { SimData }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    private struct SimData
    {
        public double Latitude;    // PLANE LATITUDE,          degrees
        public double Longitude;   // PLANE LONGITUDE,         degrees
        public int    ParkingBrake;// BRAKE PARKING INDICATOR, Bool (0/1)
    }

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

            _sc.AddToDataDefinition(Defs.SimData, "PLANE LATITUDE",          "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "PLANE LONGITUDE",         "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0f, SimConnect.SIMCONNECT_UNUSED);
            _sc.AddToDataDefinition(Defs.SimData, "BRAKE PARKING INDICATOR", "Bool",    SIMCONNECT_DATATYPE.INT32,   0f, SimConnect.SIMCONNECT_UNUSED);
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
        _initialized = false;
        _inFlight    = false;
        SetConnected(true);
    }

    private void OnQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        _inFlight    = false;
        _initialized = false;
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
        bool brakeOn = sd.ParkingBrake != 0;

        if (!_initialized)
        {
            // Establish initial state without triggering a spurious block-off.
            _lastBrake   = brakeOn;
            _initialized = true;
            return;
        }

        if (!_inFlight && _lastBrake && !brakeOn)
        {
            // Parking brake RELEASED → block-off.
            _departureIcao = NearestIcao(sd.Latitude, sd.Longitude);
            _blockOffUtc   = DateTime.UtcNow;
            _inFlight      = true;
        }
        else if (_inFlight && !_lastBrake && brakeOn)
        {
            // Parking brake SET → block-on.
            var arrivalIcao = NearestIcao(sd.Latitude, sd.Longitude);
            var blockOnUtc  = DateTime.UtcNow;

            if (_departureIcao is not null && arrivalIcao is not null &&
                !string.Equals(_departureIcao, arrivalIcao, StringComparison.OrdinalIgnoreCase))
            {
                FlightCompleted?.Invoke(this, new FlightRecord
                {
                    Date          = DateOnly.FromDateTime(DateTime.UtcNow),
                    AircraftType  = AircraftType.Airplane,
                    DepartureIcao = _departureIcao,
                    ArrivalIcao   = arrivalIcao,
                    BlockOffUtc   = _blockOffUtc,
                    BlockOnUtc    = blockOnUtc,
                });
            }

            _inFlight      = false;
            _departureIcao = null;
        }

        _lastBrake = brakeOn;
    }

    // Returns the ICAO of the nearest airport within 15 nm, or null if none found.
    private string? NearestIcao(double lat, double lon)
    {
        if (!_airports.IsLoaded) return null;

        const double margin = 0.5; // ≈30 nm rough bounding box
        var candidates = _airports.GetInBounds(
            lat - margin, lat + margin,
            lon - margin, lon + margin);

        return candidates
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
