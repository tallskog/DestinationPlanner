namespace DestinationPlanner.Helpers;

public static class GeoHelper
{
    private const double EarthRadiusNm = 3440.065;

    public static double DistanceNm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusNm * Math.Asin(Math.Sqrt(a));
    }

    public static int FeetToMeters(int feet) => (int)Math.Round(feet * 0.3048);
    public static int MetersToFeet(int meters) => (int)Math.Round(meters / 0.3048);

    // WGS84 ↔ Web Mercator (EPSG:3857) — Mapsui internal coordinate system
    private const double R = 6378137.0;
    public static double LonToMercatorX(double lon) => lon * (Math.PI / 180.0) * R;
    public static double LatToMercatorY(double lat)
    {
        double rad = lat * Math.PI / 180.0;
        return Math.Log(Math.Tan(Math.PI / 4.0 + rad / 2.0)) * R;
    }
    public static double MercatorXToLon(double x) => x / R * (180.0 / Math.PI);
    public static double MercatorYToLat(double y)
        => (2.0 * Math.Atan(Math.Exp(y / R)) - Math.PI / 2.0) * (180.0 / Math.PI);

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
}
