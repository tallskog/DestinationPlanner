using DestinationPlanner.Models;

namespace DestinationPlanner.Helpers;

public static class LandingRatingHelper
{
    // Computes derived landing fields (crosswind, runway geometry, star rating) and
    // sets them on the record in-place. touchdownLat/Lon are transient and not stored.
    public static void Enrich(FlightRecord record, double touchdownLat, double touchdownLon, IReadOnlyList<Runway>? runways)
    {
        if (record.LandingWindKts.HasValue && record.LandingWindDirection.HasValue && record.LandingHeadingDeg.HasValue)
            record.LandingCrosswindKts = ComputeCrosswind(record.LandingWindKts.Value, record.LandingWindDirection.Value, record.LandingHeadingDeg.Value);

        if (runways?.Count > 0 && record.LandingHeadingDeg.HasValue)
        {
            var geo = ComputeRunwayGeometry(touchdownLat, touchdownLon, record.LandingHeadingDeg.Value, runways);
            if (geo.HasValue)
            {
                record.LandingCenterlineDeviationFt = geo.Value.CenterlineDeviationFt;
                record.LandingTouchdownZonePct       = geo.Value.TouchdownZonePct;
            }
        }

        record.LandingStars = ComputeStars(record);
    }

    // Signed crosswind component (knots). Positive = wind from the right.
    private static double ComputeCrosswind(double windKts, double windDirDeg, double headingDeg)
    {
        double angleRad = (windDirDeg - headingDeg) * Math.PI / 180.0;
        return windKts * Math.Sin(angleRad);
    }

    private static (double CenterlineDeviationFt, double TouchdownZonePct)?
        ComputeRunwayGeometry(double lat, double lon, double headingDeg, IReadOnlyList<Runway> runways)
    {
        double bestDistM        = double.MaxValue;
        double bestCenterlineFt = 0;
        double bestTdzPct       = 0;

        foreach (var rwy in runways)
        {
            if (!rwy.LeLatitude.HasValue || !rwy.LeLongitude.HasValue ||
                !rwy.HeLatitude.HasValue || !rwy.HeLongitude.HasValue ||
                !rwy.LeHeadingDeg.HasValue)
                continue;

            // Flat-earth projection centred at the touchdown point.
            double midLat    = (rwy.LeLatitude.Value + rwy.HeLatitude.Value) / 2;
            double mPerLon   = Math.Cos(midLat * Math.PI / 180) * 111320;
            const double mPerLat = 110540;

            // Runway endpoints relative to touchdown (0, 0).
            double x1 = (rwy.LeLongitude.Value - lon) * mPerLon;
            double y1 = (rwy.LeLatitude.Value  - lat) * mPerLat;
            double x2 = (rwy.HeLongitude.Value - lon) * mPerLon;
            double y2 = (rwy.HeLatitude.Value  - lat) * mPerLat;

            double dx    = x2 - x1;
            double dy    = y2 - y1;
            double len2  = dx * dx + dy * dy;
            if (len2 < 1e-4) continue;

            // Unclamped projection parameter (negative = before le, >1 = past he).
            double t = ((-x1) * dx + (-y1) * dy) / len2;

            // Perpendicular distance to the infinite runway centreline.
            double closestX = x1 + t * dx;
            double closestY = y1 + t * dy;
            double distM    = Math.Sqrt(closestX * closestX + closestY * closestY);

            if (distM >= bestDistM) continue;
            bestDistM = distM;

            bestCenterlineFt = distM * 3.28084;

            // Determine landing threshold: the end whose heading is within 90° of the aircraft heading.
            double headingDiffLe = Math.Abs(NormalizeAngle180(headingDeg - rwy.LeHeadingDeg.Value));
            bool   leIsThreshold = headingDiffLe <= 90;
            bestTdzPct           = (leIsThreshold ? t : 1.0 - t) * 100;
        }

        if (bestDistM == double.MaxValue) return null;
        return (bestCenterlineFt, bestTdzPct);
    }

    // Returns 1–5, or null if there is insufficient data to rate.
    public static int? ComputeStars(FlightRecord r)
    {
        var components = new (double Weight, double? Value, Func<double, double> Scorer)[]
        {
            (0.30, r.LandingFpm.HasValue          ? Math.Abs(r.LandingFpm.Value) : null, ScoreFpm),
            (0.20, r.LandingGForce,                                                       ScoreGForce),
            (0.15, r.LandingBankAngleDeg.HasValue  ? Math.Abs(r.LandingBankAngleDeg.Value) : null, ScoreBank),
            (0.10, r.LandingPitchAngleDeg,                                                ScorePitch),
            (0.10, r.LandingCenterlineDeviationFt,                                        ScoreCenterline),
            (0.15, r.LandingTouchdownZonePct,                                             ScoreTdz),
        };

        double totalWeight = 0;
        double totalScore  = 0;
        foreach (var (weight, value, scorer) in components)
        {
            if (!value.HasValue) continue;
            totalWeight += weight;
            totalScore  += weight * scorer(value.Value);
        }

        if (totalWeight < 0.01) return null;

        double score = totalScore / totalWeight;
        return score switch
        {
            >= 85 => 5,
            >= 70 => 4,
            >= 50 => 3,
            >= 30 => 2,
            _     => 1,
        };
    }

    // ---- Component scorers (each returns 0–100) ----

    private static double ScoreFpm(double fpmAbs)
    {
        if (fpmAbs <= 100) return 100;
        if (fpmAbs <= 200) return Lerp(fpmAbs, 100, 200, 100, 80);
        if (fpmAbs <= 300) return Lerp(fpmAbs, 200, 300, 80, 60);
        if (fpmAbs <= 500) return Lerp(fpmAbs, 300, 500, 60, 30);
        return 0;
    }

    private static double ScoreGForce(double g)
    {
        if (g <= 1.15) return 100;
        if (g <= 1.30) return Lerp(g, 1.15, 1.30, 100, 80);
        if (g <= 1.50) return Lerp(g, 1.30, 1.50, 80, 60);
        if (g <= 1.80) return Lerp(g, 1.50, 1.80, 60, 25);
        return 0;
    }

    private static double ScoreBank(double bankAbs)
    {
        if (bankAbs <= 2)  return 100;
        if (bankAbs <= 5)  return Lerp(bankAbs, 2, 5, 100, 75);
        if (bankAbs <= 10) return Lerp(bankAbs, 5, 10, 75, 40);
        return 0;
    }

    private static double ScorePitch(double pitch)
    {
        if (pitch >= 0 && pitch <= 5)  return 100;
        if (pitch > 5  && pitch <= 10) return Lerp(pitch, 5, 10, 100, 75);
        if (pitch > 10)                return Lerp(pitch, 10, 15, 75, 25);
        if (pitch >= -2)               return Lerp(pitch, -2, 0, 50, 100);
        return 0;
    }

    private static double ScoreCenterline(double ft)
    {
        if (ft <= 10)  return 100;
        if (ft <= 25)  return Lerp(ft, 10, 25, 100, 80);
        if (ft <= 50)  return Lerp(ft, 25, 50, 80, 60);
        if (ft <= 100) return Lerp(ft, 50, 100, 60, 30);
        return 0;
    }

    private static double ScoreTdz(double pct)
    {
        if (pct < 0 || pct > 100) return 0;
        if (pct <= 33)  return 100;
        if (pct <= 50)  return Lerp(pct, 33, 50, 100, 75);
        if (pct <= 66)  return Lerp(pct, 50, 66, 75, 40);
        return Lerp(pct, 66, 100, 40, 10);
    }

    private static double Lerp(double x, double x0, double x1, double y0, double y1)
        => y0 + (y1 - y0) * (x - x0) / (x1 - x0);

    private static double NormalizeAngle180(double angle)
    {
        while (angle >  180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
}
