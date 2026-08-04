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
        var coordinates = CelestialMath.GetHorizontalCoordinates(star.RightAscensionDeg, star.DeclinationDeg, latitudeDeg, localSiderealDeg);
        if (coordinates.AltitudeDeg <= visualHorizonCutoffDeg)
        {
            return null;
        }

        var fadeStart = Math.Min(visualHorizonCutoffDeg, horizonFadeBandDeg - 0.001);
        var horizonFactor = Math.Clamp((coordinates.AltitudeDeg - fadeStart) / (horizonFadeBandDeg - fadeStart), 0.0, 1.0);
        var brightness = Math.Clamp(StarBrightnessFromMagnitude(star.VisualMagnitude) * brightnessBias * horizonFactor, 0.0, 1.0);
        var (directionX, directionY, directionZ) = CalculateDirection(coordinates.AzimuthDeg, coordinates.AltitudeDeg);
        var size = CalculateSize(star.VisualMagnitude);

        return new RenderedStar(
            star.Hip,
            star.VisualMagnitude,
            coordinates.AzimuthDeg,
            coordinates.AltitudeDeg,
            brightness,
            EstimateColorTemperature(star.BvColorIndex),
            star.IsGuideStar,
            directionX,
            directionY,
            directionZ,
            size);
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

    private static double CalculateSize(double visualMagnitude)
    {
        var brightness = StarBrightnessFromMagnitude(visualMagnitude);
        return Math.Clamp(12.5 * (0.7 + (brightness * 0.78)), 7.0, 24.0);
    }

    private static double StarBrightnessFromMagnitude(double visualMagnitude)
    {
        var dimming = Math.Clamp((visualMagnitude - 0.4) / 5.6, 0.0, 1.0);
        return 1.0 - (dimming * 0.64);
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
}
