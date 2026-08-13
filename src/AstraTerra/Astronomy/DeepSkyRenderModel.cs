namespace AstraTerra.Astronomy;

public readonly record struct DeepSkyDirection(double X, double Y, double Z);

public sealed record RenderedDeepSkyObject(
    string Id,
    string DisplayName,
    double AngularSizeDeg,
    double Brightness,
    float TintR,
    float TintG,
    float TintB,
    IReadOnlyList<string> TexturePaths,
    IReadOnlyList<DeepSkyDirection> QuadCorners
);

public static class DeepSkyRenderModel
{
    /// <summary>Altitude at which a plate reaches full brightness, fading in below it.</summary>
    public const double DefaultHorizonFadeBandDeg = 12.0;

    /// <summary>
    /// Altitude below which a plate is dropped. Tighter than the starfield's cutoff because a
    /// deep-sky plate covers degrees of sky rather than a point, so half of it would still be
    /// hanging below the terrain.
    /// </summary>
    public const double DefaultVisualHorizonCutoffDeg = -8.0;

    public static IReadOnlyList<RenderedDeepSkyObject> ProjectVisibleObjects(
        IEnumerable<DeepSkyObjectEntry> objects,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = DefaultVisualHorizonCutoffDeg)
    {
        return objects
            .Select(entry => Project(entry, latitudeDeg, localSiderealDeg, brightnessBias, horizonFadeBandDeg, visualHorizonCutoffDeg))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .OrderBy(entry => entry.Brightness)
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Places a plate on the sky. Deep-sky objects share the starfield's horizon handling and world
    /// directions but not its magnitude curve: a nebula's brightness is authored per object, being a
    /// surface brightness spread over degrees rather than a point source's magnitude.
    /// </summary>
    public static RenderedDeepSkyObject? Project(
        DeepSkyObjectEntry entry,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = DefaultVisualHorizonCutoffDeg)
    {
        var coordinates = CelestialMath.GetHorizontalCoordinates(entry.RightAscensionDeg, entry.DeclinationDeg, latitudeDeg, localSiderealDeg);
        if (coordinates.AltitudeDeg <= visualHorizonCutoffDeg)
        {
            return null;
        }

        var horizonFactor = SkyProjection.GetHorizonFadeFactor(
            coordinates.AltitudeDeg,
            horizonFadeBandDeg,
            visualHorizonCutoffDeg);
        var brightness = Math.Clamp(entry.Brightness * brightnessBias * horizonFactor, 0.0, 1.0);
        if (brightness <= 0.001)
        {
            return null;
        }

        if (entry.WorldCoords is null || entry.WorldCoords.Count != 4)
        {
            return null;
        }

        var quadCorners = entry.WorldCoords
            .Select(corner =>
            {
                var (x, y, z) = SkyProjection.GetWorldDirection(corner, latitudeDeg, localSiderealDeg);
                return new DeepSkyDirection(x, y, z);
            })
            .ToList();

        return new RenderedDeepSkyObject(
            entry.Id,
            entry.DisplayName,
            entry.AngularSizeDeg,
            brightness,
            entry.TintR,
            entry.TintG,
            entry.TintB,
            BuildTexturePathList(entry),
            quadCorners);
    }

    private static IReadOnlyList<string> BuildTexturePathList(DeepSkyObjectEntry entry)
    {
        return new[] { entry.TexturePath }
            .Concat(entry.FallbackTexturePaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
