namespace AstraTerra.Astronomy;

public sealed record RenderedStar(
    int Hip,
    double VisualMagnitude,
    double AzimuthDeg,
    double AltitudeDeg,
    double Brightness,
    double ColorTemperatureK,
    bool IsGuideStar,
    double DirectionX,
    double DirectionY,
    double DirectionZ,
    double Size
);

public static class StarRenderModel
{
    public static IReadOnlyList<RenderedStar> ProjectVisibleStars(
        IEnumerable<StarCatalogEntry> stars,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = 10.0,
        double visualHorizonCutoffDeg = -15.0)
    {
        return stars.Select(star => Project(star, latitudeDeg, localSiderealDeg, brightnessBias, horizonFadeBandDeg, visualHorizonCutoffDeg))
            .Where(star => star is not null)
            .Select(star => star!)
            .OrderByDescending(star => star.Brightness)
            .ThenBy(star => star.Hip)
            .ToList();
    }

    public static RenderedStar? Project(
        StarCatalogEntry star,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = 10.0,
        double visualHorizonCutoffDeg = -15.0)
    {
        var altitude = CelestialMath.ClassifyAltitudeDeg(star.RightAscensionDeg, star.DeclinationDeg, latitudeDeg, localSiderealDeg);
        if (altitude <= visualHorizonCutoffDeg)
        {
            return null;
        }

        var azimuth = CalculateAzimuthDeg(star.RightAscensionDeg, star.DeclinationDeg, latitudeDeg, localSiderealDeg);
        var fadeStart = Math.Min(visualHorizonCutoffDeg, horizonFadeBandDeg - 0.001);
        var horizonFactor = Math.Clamp((altitude - fadeStart) / (horizonFadeBandDeg - fadeStart), 0.0, 1.0);
        var brightness = Math.Clamp(StarBrightnessFromMagnitude(star.VisualMagnitude) * brightnessBias * horizonFactor, 0.0, 1.0);
        var (directionX, directionY, directionZ) = CalculateDirection(azimuth, altitude);
        var size = CalculateSize(star.VisualMagnitude, brightnessBias);

        return new RenderedStar(
            star.Hip,
            star.VisualMagnitude,
            azimuth,
            altitude,
            brightness,
            EstimateColorTemperature(star.BvColorIndex),
            star.IsGuideStar,
            directionX,
            directionY,
            directionZ,
            size);
    }

    private static double CalculateAzimuthDeg(double rightAscensionDeg, double declinationDeg, double latitudeDeg, double localSiderealDeg)
    {
        var hourAngle = ToRadians(CelestialMath.NormalizeDegrees(localSiderealDeg - rightAscensionDeg));
        var declination = ToRadians(declinationDeg);
        var latitude = ToRadians(latitudeDeg);
        var y = -Math.Sin(hourAngle);
        var x = Math.Tan(declination) * Math.Cos(latitude) - Math.Sin(latitude) * Math.Cos(hourAngle);
        return CelestialMath.NormalizeDegrees(ToDegrees(Math.Atan2(y, x)));
    }

    private static (double X, double Y, double Z) CalculateDirection(double azimuthDeg, double altitudeDeg)
    {
        var azimuth = ToRadians(azimuthDeg);
        var altitude = ToRadians(altitudeDeg);
        var horizontal = Math.Cos(altitude);

        return (
            horizontal * Math.Sin(azimuth),
            Math.Sin(altitude),
            -horizontal * Math.Cos(azimuth));
    }

    private static double CalculateSize(double visualMagnitude, double brightnessBias)
    {
        var brightness = StarBrightnessFromMagnitude(visualMagnitude);
        var biasFactor = Math.Clamp(Math.Sqrt(Math.Max(0.0, brightnessBias)), 0.65, 1.75);
        return Math.Clamp(12.5 * (0.9 + (brightness * 0.2)) * biasFactor, 7.0, 24.0);
    }

    private static double StarBrightnessFromMagnitude(double visualMagnitude)
    {
        var dimming = Math.Clamp((visualMagnitude - 0.4) / 5.6, 0.0, 1.0);
        return 1.0 - (dimming * 0.8);
    }

    private static double EstimateColorTemperature(double? bvColorIndex)
    {
        if (bvColorIndex is null)
        {
            return 6500;
        }

        var bv = Math.Clamp(bvColorIndex.Value, -0.4, 2.0);
        return 4600.0 * ((1.0 / ((0.92 * bv) + 1.7)) + (1.0 / ((0.92 * bv) + 0.62)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
