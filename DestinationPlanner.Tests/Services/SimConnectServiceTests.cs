using System.Runtime.InteropServices;
using DestinationPlanner.Services;

namespace DestinationPlanner.Tests.Services;

// Pure helpers only — live SimConnect/MSFS behavior is verified manually (BUG-16).
public class SimConnectServiceTests
{
    [Theory]
    [InlineData(0, 0.0, false)]
    [InlineData(1, 0.0, true)]   // standard sim var (most aircraft)
    [InlineData(0, 1.0, true)]   // Fenix A320: L:S_MIP_PARKING_BRAKE set, sim var stays 0
    [InlineData(1, 1.0, true)]
    public void IsParkingBrakeSet_EitherSourceCounts(int indicator, double fenix, bool expected)
    {
        Assert.Equal(expected, SimConnectService.IsParkingBrakeSet(indicator, fenix));
    }

    [Fact]
    public void FacilityRunwayData_MatchesSdkWireLayout()
    {
        // LATITUDE/LONGITUDE FLOAT64 (2×8) + HEADING/LENGTH FLOAT32 (2×4) + 4×INT32 = 40 bytes.
        Assert.Equal(40, Marshal.SizeOf<SimConnectService.FacilityRunwayData>());
    }

    [Fact]
    public void FacilityRunwayData_UnmarshalsHeadingAndLengthFromSdkBytes()
    {
        // Build the bytes exactly as SimConnect sends them for a 3000 m, 090° runway "09/27".
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(60.0));
        bytes.AddRange(BitConverter.GetBytes(25.0));
        bytes.AddRange(BitConverter.GetBytes(90f));
        bytes.AddRange(BitConverter.GetBytes(3000f));
        bytes.AddRange(BitConverter.GetBytes(9));
        bytes.AddRange(BitConverter.GetBytes(0));
        bytes.AddRange(BitConverter.GetBytes(27));
        bytes.AddRange(BitConverter.GetBytes(0));

        var handle = GCHandle.Alloc(bytes.ToArray(), GCHandleType.Pinned);
        SimConnectService.FacilityRunwayData fr;
        try { fr = Marshal.PtrToStructure<SimConnectService.FacilityRunwayData>(handle.AddrOfPinnedObject()); }
        finally { handle.Free(); }

        var rwy = SimConnectService.ConvertFacilityRunway(fr);

        Assert.Equal("09/27", rwy.Ident);
        Assert.Equal(9843, rwy.LengthFt);
        Assert.Equal(90, rwy.LeHeadingDeg!.Value, precision: 3);
        // Thresholds sit ±1500 m east/west of the center point on the same latitude.
        Assert.Equal(60.0, rwy.LeLatitude!.Value, precision: 6);
        Assert.True(rwy.LeLongitude < 25.0 && rwy.HeLongitude > 25.0);
    }
}
