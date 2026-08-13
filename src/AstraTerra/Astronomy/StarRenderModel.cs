namespace AstraTerra.Astronomy;

/// <summary>
/// A catalog star placed in the sky: the shared <see cref="RenderedBody"/> plus the things only a
/// star has.
/// </summary>
/// <remarks>
/// The body is exposed directly so renderer and instrument code can take it and work equally well
/// on a planet or a comet, while the flattened properties keep every existing call site reading a
/// star the way it always has.
/// </remarks>
public sealed record RenderedStar(
    int Hip,
    RenderedBody Body,
    double ColorTemperatureK,
    bool IsGuideStar
)
{
    public double VisualMagnitude => Body.VisualMagnitude;

    public double AzimuthDeg => Body.AzimuthDeg;

    public double AltitudeDeg => Body.AltitudeDeg;

    public double Brightness => Body.Brightness;

    public double DirectionX => Body.DirectionX;

    public double DirectionY => Body.DirectionY;

    public double DirectionZ => Body.DirectionZ;

    public double Size => Body.Size;
}

public static class StarRenderModel
{
    public static IReadOnlyList<RenderedStar> ProjectVisibleStars(
        IEnumerable<StarCatalogEntry> stars,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        return stars.Select(star => Project(star, latitudeDeg, localSiderealDeg, brightnessBias, horizonFadeBandDeg, visualHorizonCutoffDeg))
            .Where(star => star is not null)
            .Select(star => star!)
            .OrderByDescending(star => star.Brightness)
            .ThenBy(star => star.Hip)
            .ToList();
    }

    /// <summary>
    /// Resolves a star's fixed position and hands the rest to <see cref="SkyProjection"/>. Only the
    /// first step is star-specific; everything a planet would do differently is upstream of here.
    /// </summary>
    public static RenderedStar? Project(
        StarCatalogEntry star,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        var body = SkyProjection.Project(
            new EquatorialCoordinates(star.RightAscensionDeg, star.DeclinationDeg),
            star.VisualMagnitude,
            latitudeDeg,
            localSiderealDeg,
            brightnessBias,
            horizonFadeBandDeg,
            visualHorizonCutoffDeg);

        return body is null
            ? null
            : new RenderedStar(
                star.Hip,
                body,
                EstimateColorTemperature(star.BvColorIndex),
                star.IsGuideStar);
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
}
