using AstraTerra.Astronomy;
using AstraTerra.Config;

namespace AstraTerra.Client.Rendering;

public enum SkyCoordinateGridSystem
{
    Horizontal,
    Equatorial
}

public readonly record struct SkyGridDirection(double X, double Y, double Z);

public readonly record struct SkyGridSegment(
    SkyCoordinateGridSystem System,
    bool IsReferenceLine,
    SkyGridDirection Start,
    SkyGridDirection End);

public static class SkyCoordinateGridModel
{
    private const double HorizontalParallelSpacingDeg = 15.0;
    private const double HorizontalMeridianSpacingDeg = 30.0;
    private const double DeclinationSpacingDeg = 15.0;
    private const double RightAscensionSpacingDeg = 30.0;
    private const double CurveSampleSpacingDeg = 5.0;
    private const double HorizonEpsilon = 0.000001;

    public static IReadOnlyList<SkyGridSegment> Build(
        SkyGridMode mode,
        double latitudeDeg,
        double localSiderealDeg)
    {
        if (mode == SkyGridMode.None)
        {
            return [];
        }

        var segments = new List<SkyGridSegment>();
        if (SkyGridModeParser.ShowsHorizontal(mode))
        {
            AddHorizontalGrid(segments);
        }

        if (SkyGridModeParser.ShowsEquatorial(mode))
        {
            AddEquatorialGrid(segments, latitudeDeg, localSiderealDeg);
        }

        return segments;
    }

    private static void AddHorizontalGrid(List<SkyGridSegment> segments)
    {
        for (var altitude = 0.0; altitude < 90.0; altitude += HorizontalParallelSpacingDeg)
        {
            AddClosedHorizontalCurve(
                segments,
                altitude,
                isReferenceLine: altitude == 0.0);
        }

        for (var azimuth = 0.0; azimuth < 360.0; azimuth += HorizontalMeridianSpacingDeg)
        {
            var isReferenceLine = IsCardinalAzimuth(azimuth);
            for (var altitude = 0.0; altitude < 90.0; altitude += CurveSampleSpacingDeg)
            {
                segments.Add(new SkyGridSegment(
                    SkyCoordinateGridSystem.Horizontal,
                    isReferenceLine,
                    DirectionFromHorizontal(azimuth, altitude),
                    DirectionFromHorizontal(azimuth, Math.Min(90.0, altitude + CurveSampleSpacingDeg))));
            }
        }
    }

    private static void AddClosedHorizontalCurve(
        List<SkyGridSegment> segments,
        double altitudeDeg,
        bool isReferenceLine)
    {
        for (var azimuth = 0.0; azimuth < 360.0; azimuth += CurveSampleSpacingDeg)
        {
            segments.Add(new SkyGridSegment(
                SkyCoordinateGridSystem.Horizontal,
                isReferenceLine,
                DirectionFromHorizontal(azimuth, altitudeDeg),
                DirectionFromHorizontal(
                    CelestialMath.NormalizeDegrees(azimuth + CurveSampleSpacingDeg),
                    altitudeDeg)));
        }
    }

    private static void AddEquatorialGrid(
        List<SkyGridSegment> segments,
        double latitudeDeg,
        double localSiderealDeg)
    {
        for (var declination = -75.0; declination <= 75.0; declination += DeclinationSpacingDeg)
        {
            for (var rightAscension = 0.0; rightAscension < 360.0; rightAscension += CurveSampleSpacingDeg)
            {
                AddVisibleEquatorialSegment(
                    segments,
                    latitudeDeg,
                    localSiderealDeg,
                    rightAscension,
                    declination,
                    CelestialMath.NormalizeDegrees(rightAscension + CurveSampleSpacingDeg),
                    declination,
                    isReferenceLine: declination == 0.0);
            }
        }

        for (var rightAscension = 0.0; rightAscension < 360.0; rightAscension += RightAscensionSpacingDeg)
        {
            var isReferenceLine = rightAscension == 0.0 || rightAscension == 180.0;
            for (var declination = -90.0; declination < 90.0; declination += CurveSampleSpacingDeg)
            {
                AddVisibleEquatorialSegment(
                    segments,
                    latitudeDeg,
                    localSiderealDeg,
                    rightAscension,
                    declination,
                    rightAscension,
                    Math.Min(90.0, declination + CurveSampleSpacingDeg),
                    isReferenceLine);
            }
        }
    }

    private static void AddVisibleEquatorialSegment(
        List<SkyGridSegment> segments,
        double latitudeDeg,
        double localSiderealDeg,
        double startRightAscensionDeg,
        double startDeclinationDeg,
        double endRightAscensionDeg,
        double endDeclinationDeg,
        bool isReferenceLine)
    {
        var startCoordinates = CelestialMath.GetHorizontalCoordinates(
            startRightAscensionDeg,
            startDeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        var endCoordinates = CelestialMath.GetHorizontalCoordinates(
            endRightAscensionDeg,
            endDeclinationDeg,
            latitudeDeg,
            localSiderealDeg);

        var startVisible = startCoordinates.AltitudeDeg >= -HorizonEpsilon;
        var endVisible = endCoordinates.AltitudeDeg >= -HorizonEpsilon;
        if (!startVisible && !endVisible)
        {
            return;
        }

        var start = DirectionFromHorizontal(startCoordinates.AzimuthDeg, startCoordinates.AltitudeDeg);
        var end = DirectionFromHorizontal(endCoordinates.AzimuthDeg, endCoordinates.AltitudeDeg);
        if (startVisible != endVisible)
        {
            var horizon = InterpolateHorizonCrossing(
                start,
                startCoordinates.AltitudeDeg,
                end,
                endCoordinates.AltitudeDeg);
            if (startVisible)
            {
                end = horizon;
            }
            else
            {
                start = horizon;
            }
        }

        segments.Add(new SkyGridSegment(
            SkyCoordinateGridSystem.Equatorial,
            isReferenceLine,
            start,
            end));
    }

    private static SkyGridDirection InterpolateHorizonCrossing(
        SkyGridDirection start,
        double startAltitudeDeg,
        SkyGridDirection end,
        double endAltitudeDeg)
    {
        var denominator = startAltitudeDeg - endAltitudeDeg;
        var t = Math.Abs(denominator) <= HorizonEpsilon
            ? 0.5
            : Math.Clamp(startAltitudeDeg / denominator, 0.0, 1.0);
        var x = start.X + ((end.X - start.X) * t);
        var z = start.Z + ((end.Z - start.Z) * t);
        var horizontalLength = Math.Sqrt((x * x) + (z * z));
        if (horizontalLength <= HorizonEpsilon)
        {
            return new SkyGridDirection(0.0, 0.0, -1.0);
        }

        return new SkyGridDirection(x / horizontalLength, 0.0, z / horizontalLength);
    }

    private static SkyGridDirection DirectionFromHorizontal(double azimuthDeg, double altitudeDeg)
    {
        var azimuth = ToRadians(azimuthDeg);
        var altitude = ToRadians(altitudeDeg);
        var horizontal = Math.Cos(altitude);
        return new SkyGridDirection(
            horizontal * Math.Sin(azimuth),
            Math.Sin(altitude),
            -horizontal * Math.Cos(azimuth));
    }

    private static bool IsCardinalAzimuth(double azimuthDeg)
        => Math.Abs(azimuthDeg % 90.0) <= HorizonEpsilon;

    private static double ToRadians(double degrees)
        => degrees * Math.PI / 180.0;
}
