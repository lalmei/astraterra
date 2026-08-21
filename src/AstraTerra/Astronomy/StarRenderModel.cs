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
public readonly record struct RenderedStar(
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
    /// <summary>
    /// Brightest first, ties broken by catalog number so the order is stable frame to frame.
    /// </summary>
    private static readonly Comparison<RenderedStar> DrawOrder = static (left, right) =>
    {
        var byBrightness = right.Brightness.CompareTo(left.Brightness);
        return byBrightness != 0 ? byBrightness : left.Hip.CompareTo(right.Hip);
    };

    public static IReadOnlyList<RenderedStar> ProjectVisibleStars(
        IEnumerable<StarCatalogEntry> stars,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        var projected = new List<RenderedStar>();
        ProjectVisibleStars(stars, latitudeDeg, localSiderealDeg, brightnessBias, projected, horizonFadeBandDeg, visualHorizonCutoffDeg);
        return projected;
    }

    /// <summary>
    /// Fills a caller-owned list rather than building one, for the render loop.
    /// </summary>
    /// <remarks>
    /// The sky pass runs this over the whole catalog every frame it draws. Written as one pass into
    /// a reused buffer with an in-place sort, because the LINQ chain it replaced allocated about
    /// 440 KiB and cost over 10 ms per frame with five thousand stars — a stutter on its own, and a
    /// gigabyte a minute of garbage for the collector to chase.
    /// </remarks>
    public static void ProjectVisibleStars(
        IEnumerable<StarCatalogEntry> stars,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        List<RenderedStar> destination,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        ArgumentNullException.ThrowIfNull(stars);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        if (stars is IReadOnlyList<StarCatalogEntry> indexable)
        {
            // Indexed rather than foreach: an interface enumerator on the hot path costs an
            // allocation and a virtual call per star.
            for (var index = 0; index < indexable.Count; index++)
            {
                Add(indexable[index]);
            }
        }
        else
        {
            foreach (var star in stars)
            {
                Add(star);
            }
        }

        destination.Sort(DrawOrder);
        return;

        void Add(StarCatalogEntry star)
        {
            if (Project(star, latitudeDeg, localSiderealDeg, brightnessBias, horizonFadeBandDeg, visualHorizonCutoffDeg) is { } projected)
            {
                destination.Add(projected);
            }
        }
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

        return body is not { } projectedBody
            ? null
            : new RenderedStar(
                star.Hip,
                projectedBody,
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
