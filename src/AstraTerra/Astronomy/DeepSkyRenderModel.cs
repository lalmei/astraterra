namespace AstraTerra.Astronomy;

public sealed record RenderedDeepSkyObject(
    string Id,
    string DisplayName,
    double AngularSizeDeg,
    double Brightness,
    float TintR,
    float TintG,
    float TintB,
    IReadOnlyList<string> TexturePaths,
    double DirectionX,
    double DirectionY,
    double DirectionZ
);

public static class DeepSkyRenderModel
{
    public static IReadOnlyList<RenderedDeepSkyObject> ProjectVisibleObjects(
        IEnumerable<DeepSkyObjectEntry> objects,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = 12.0,
        double visualHorizonCutoffDeg = -8.0)
    {
        return objects
            .Select(entry => Project(entry, latitudeDeg, localSiderealDeg, brightnessBias, horizonFadeBandDeg, visualHorizonCutoffDeg))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .OrderBy(entry => entry.Brightness)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static RenderedDeepSkyObject? Project(
        DeepSkyObjectEntry entry,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = 12.0,
        double visualHorizonCutoffDeg = -8.0)
    {
        var coordinates = CelestialMath.GetHorizontalCoordinates(entry.RightAscensionDeg, entry.DeclinationDeg, latitudeDeg, localSiderealDeg);
        if (coordinates.AltitudeDeg <= visualHorizonCutoffDeg)
        {
            return null;
        }

        var fadeStart = Math.Min(visualHorizonCutoffDeg, horizonFadeBandDeg - 0.001);
        var horizonFactor = Math.Clamp((coordinates.AltitudeDeg - fadeStart) / (horizonFadeBandDeg - fadeStart), 0.0, 1.0);
        var brightness = Math.Clamp(entry.Brightness * brightnessBias * horizonFactor, 0.0, 1.0);
        if (brightness <= 0.001)
        {
            return null;
        }

        var (directionX, directionY, directionZ) = CalculateDirection(coordinates.AzimuthDeg, coordinates.AltitudeDeg);
        return new RenderedDeepSkyObject(
            entry.Id,
            entry.DisplayName,
            entry.AngularSizeDeg,
            brightness,
            entry.TintR,
            entry.TintG,
            entry.TintB,
            BuildTexturePathList(entry),
            directionX,
            directionY,
            directionZ);
    }

    private static IReadOnlyList<string> BuildTexturePathList(DeepSkyObjectEntry entry)
    {
        return new[] { entry.TexturePath }
            .Concat(entry.FallbackTexturePaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
