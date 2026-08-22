namespace AstraTerra.Astronomy;

/// <summary>A unit direction in world space, as <see cref="SkyProjection.GetWorldDirection"/> returns one.</summary>
public readonly record struct SkyDirection(double X, double Y, double Z);

/// <summary>
/// A comet placed in the sky: the coma as any other body, plus the tail.
/// </summary>
/// <param name="Prominence">
/// 1 at perihelion and 0 at the edges of the apparition. Drives how solidly the coma and tail draw,
/// so the comet arrives and leaves as a fade rather than as a switch. The magnitude curve alone
/// cannot do this — the mod's brightness scale is deliberately compressed and never reaches zero, so
/// a comet authored at magnitude 8 still draws at a third of full brightness.
/// </param>
/// <param name="TailAxis">
/// The direction the tail runs from the coma, along the sky. Anti-sunward, never along the comet's
/// motion.
/// </param>
public sealed record RenderedComet(
    string Id,
    string DisplayName,
    RenderedBody Body,
    double Prominence,
    double TailLengthDeg,
    SkyDirection TailAxis,
    float TintR,
    float TintG,
    float TintB
)
{
    public double VisualMagnitude => Body.VisualMagnitude;

    public double AzimuthDeg => Body.AzimuthDeg;

    public double AltitudeDeg => Body.AltitudeDeg;

    public double Brightness => Body.Brightness;

    public double Size => Body.Size;

    public SkyDirection ComaDirection => new(Body.DirectionX, Body.DirectionY, Body.DirectionZ);
}

/// <summary>
/// Places the comets that are currently here, and works out which way their tails lie.
/// </summary>
/// <remarks>
/// An instance rather than a static class for the same reason <see cref="PlanetRenderModel"/> is
/// one: each comet holds a cached ephemeris, and re-solving an authored track every frame would be
/// work for nothing.
/// </remarks>
public sealed class CometRenderModel
{
    private readonly IReadOnlyList<CometEphemeris> comets;

    public CometRenderModel(CometCatalog catalog, int daysPerYear)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        comets = [.. catalog.Comets.Select(comet => new CometEphemeris(comet, daysPerYear))];
    }

    public int Count => comets.Count;

    public IReadOnlyList<CometEphemeris> Ephemerides => comets;

    /// <summary>
    /// Every comet that is both mid-apparition and above the drawing horizon, brightest first.
    /// </summary>
    /// <param name="sunDirection">
    /// Where the sun is in world space right now, from <c>IGameCalendar.GetSunPosition</c>. Below the
    /// horizon is the normal case — it is night — and works exactly as well: what matters is the
    /// angle between the comet and the sun, not whether either has risen.
    /// </param>
    public IReadOnlyList<RenderedComet> ProjectVisibleComets(
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        SkyDirection sunDirection,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        return comets
            .Select(ephemeris => Project(
                ephemeris,
                totalDays,
                latitudeDeg,
                localSiderealDeg,
                brightnessBias,
                sunDirection,
                horizonFadeBandDeg,
                visualHorizonCutoffDeg))
            .Where(comet => comet is not null)
            .Select(comet => comet!)
            .OrderByDescending(comet => comet.Brightness)
            .ThenBy(comet => comet.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Places one comet, or returns null when it is not due, has faded out, or is below the horizon.
    /// </summary>
    public static RenderedComet? Project(
        CometEphemeris ephemeris,
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        SkyDirection sunDirection,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);

        var apparition = ephemeris.ApparitionAt(totalDays);
        if (!apparition.IsVisible)
        {
            return null;
        }

        var body = SkyProjection.Project(
            ephemeris.PositionAt(totalDays),
            ephemeris.MagnitudeAt(totalDays),
            latitudeDeg,
            localSiderealDeg,
            brightnessBias,
            horizonFadeBandDeg,
            visualHorizonCutoffDeg);
        if (body is not { } projectedBody)
        {
            return null;
        }

        var comet = ephemeris.Comet;
        return new RenderedComet(
            comet.Id,
            comet.DisplayName,
            projectedBody,
            apparition.Closeness,
            CometApparitionSchedule.GetTailLengthDeg(comet, apparition.Closeness),
            GetTailAxis(new SkyDirection(projectedBody.DirectionX, projectedBody.DirectionY, projectedBody.DirectionZ), sunDirection),
            comet.TintR,
            comet.TintG,
            comet.TintB);
    }

    /// <summary>
    /// Which way the tail lies: directly away from the sun, projected onto the sky at the comet.
    /// </summary>
    /// <remarks>
    /// This is the one piece of comet behaviour worth getting right, because it is the piece a
    /// player can check. A tail does not trail behind the comet like smoke — sunlight and the solar
    /// wind push it, so it points away from the sun whichever way the comet is travelling, and it
    /// swings right around as the comet rounds perihelion. Drawing it along the motion instead would
    /// look plausible and be wrong in a way anyone who has seen a comet would notice.
    /// <para>
    /// Taking the component of the anti-sunward direction that lies along the sky at the comet is
    /// what turns "away from the sun in space" into "which way to draw on the sphere".
    /// </para>
    /// </remarks>
    public static SkyDirection GetTailAxis(SkyDirection comaDirection, SkyDirection sunDirection)
    {
        var coma = Normalize(comaDirection);
        var sun = Normalize(sunDirection);

        var alignment = (sun.X * coma.X) + (sun.Y * coma.Y) + (sun.Z * coma.Z);
        var axis = new SkyDirection(
            (coma.X * alignment) - sun.X,
            (coma.Y * alignment) - sun.Y,
            (coma.Z * alignment) - sun.Z);

        var length = Math.Sqrt((axis.X * axis.X) + (axis.Y * axis.Y) + (axis.Z * axis.Z));
        if (length > 1e-6)
        {
            return new SkyDirection(axis.X / length, axis.Y / length, axis.Z / length);
        }

        // The comet is sitting almost exactly on, or exactly opposite, the sun. The tail is pointed
        // at or away from the observer and has no direction across the sky at all; any perpendicular
        // will do, and the foreshortening the renderer applies takes care of the rest.
        return AnyPerpendicular(coma);
    }

    private static SkyDirection AnyPerpendicular(SkyDirection direction)
    {
        // Crossing with whichever axis the direction leans on least keeps this well conditioned.
        var reference = Math.Abs(direction.Y) < 0.9
            ? new SkyDirection(0, 1, 0)
            : new SkyDirection(1, 0, 0);

        return Normalize(new SkyDirection(
            (direction.Y * reference.Z) - (direction.Z * reference.Y),
            (direction.Z * reference.X) - (direction.X * reference.Z),
            (direction.X * reference.Y) - (direction.Y * reference.X)));
    }

    private static SkyDirection Normalize(SkyDirection direction)
    {
        var length = Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));
        return length <= 0
            ? new SkyDirection(0, 1, 0)
            : new SkyDirection(direction.X / length, direction.Y / length, direction.Z / length);
    }
}
