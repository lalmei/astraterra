namespace AstraTerra.Astronomy;

/// <summary>
/// A near body placed for a moment: where to draw it, how wide, and where its light is coming from.
/// </summary>
/// <param name="SunDirection">
/// Unit direction to the sun. The bodies this places are far closer than the sun is, so the
/// direction from the observer and the direction from the body are the same to well under a degree,
/// and one vector lights every body in the sky.
/// </param>
/// <param name="IlluminatedFraction">
/// How much of the disc the observer can see lit, 0 at new and 1 at full. Not used to draw the
/// terminator -- the mesh does that per vertex -- but it is what a moon's brightness in the
/// landscape should follow.
/// </param>
/// <param name="SeparationRatio">
/// How far off the body is, as a multiple of the parent's distance. One for a body that has no
/// orbit to work it out from, which is also where the parent itself sits, so a sibling round the
/// far side comes out above one and is drawn behind the parent.
/// </param>
public sealed record PlacedNearBody(
    NearBodyEntry Body,
    SkyDirection Direction,
    SkyDirection SunDirection,
    double AltitudeDeg,
    double AngularDiameterDeg,
    double IlluminatedFraction,
    double SeparationRatio = 1.0
);

/// <summary>
/// Places near bodies for an observer: hour angle forward to the moment, then into the sky the same
/// way every other body goes.
/// </summary>
/// <remarks>
/// Nothing here draws or caches. A near body's position changes slowly -- the fixed ones do not
/// change at all -- but there are only ever a handful of them, so they are placed per frame rather
/// than remembered, which keeps the sun direction they are lit by exactly current.
/// </remarks>
public static class NearBodyRenderModel
{
    /// <summary>
    /// How far below the horizon a body's centre may sit before it is dropped. Its own radius is
    /// allowed on top: a body tens of degrees wide is still half up with its centre well under.
    /// </summary>
    public const double HorizonCutoffDeg = -2.0;

    /// <summary>
    /// The closest two bodies are allowed to come before the drawn size stops growing. Hill
    /// separation keeps real siblings orders of magnitude further apart than this; it is here so
    /// nothing divides by zero.
    /// </summary>
    public const double MinSeparationRatio = 1e-3;

    public static IReadOnlyList<PlacedNearBody> Place(
        NearBodyCatalog catalog,
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        SkyDirection sunDirection)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Bodies.Count == 0)
        {
            return [];
        }

        var placed = new List<PlacedNearBody>(catalog.Bodies.Count);
        foreach (var body in catalog.Bodies)
        {
            if (Place(body, totalDays, latitudeDeg, localSiderealDeg, sunDirection) is { } visible)
            {
                placed.Add(visible);
            }
        }

        // Farthest first, so a sibling passing in front of the parent planet is drawn over it and
        // one round the far side goes behind it. Depth is off in this pass, so the order is the
        // occultation. Bodies at the same distance fall back to the wider one first, which is the
        // parent when nothing carries an orbit.
        placed.Sort(static (a, b) =>
        {
            var byDistance = b.SeparationRatio.CompareTo(a.SeparationRatio);
            return byDistance != 0 ? byDistance : b.AngularDiameterDeg.CompareTo(a.AngularDiameterDeg);
        });
        return placed;
    }

    public static PlacedNearBody? Place(
        NearBodyEntry body,
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        SkyDirection sunDirection)
    {
        ArgumentNullException.ThrowIfNull(body);

        var separation = SeparationRatio(body, totalDays);
        var angularDiameter = body.AngularDiameterDeg / separation;
        var rightAscension = RightAscensionDeg(body, totalDays, localSiderealDeg);
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            rightAscension,
            body.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        if (horizontal.AltitudeDeg < HorizonCutoffDeg - (angularDiameter * 0.5))
        {
            return null;
        }

        var (x, y, z) = SkyProjection.GetWorldDirection(horizontal.AzimuthDeg, horizontal.AltitudeDeg);
        var direction = new SkyDirection(x, y, z);
        return new PlacedNearBody(
            body,
            direction,
            sunDirection,
            horizontal.AltitudeDeg,
            angularDiameter,
            IlluminatedFraction(direction, sunDirection),
            separation);
    }

    /// <summary>
    /// Where the body sits among the stars right now. A body with no hour-angle rate stands still
    /// over the ground, so its right ascension has to run with the sidereal clock to stay there.
    /// </summary>
    public static double RightAscensionDeg(NearBodyEntry body, double totalDays, double localSiderealDeg)
    {
        ArgumentNullException.ThrowIfNull(body);
        return CelestialMath.NormalizeDegrees(localSiderealDeg - HourAngleDeg(body, totalDays));
    }

    /// <summary>Where the body hangs at this moment, measured west from the meridian.</summary>
    public static double HourAngleDeg(NearBodyEntry body, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Orbit is not { } orbit)
        {
            return body.HourAngleDeg + (body.HourAngleRateDegPerDay * totalDays);
        }

        return orbit.AnchorHourAngleDeg + ElongationDeg(orbit.DistanceRatio, PhaseDeg(orbit, totalDays));
    }

    /// <summary>How far round its own orbit past the observer the sibling has come.</summary>
    public static double PhaseDeg(NearBodyOrbit orbit, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(orbit);
        return orbit.PhaseDeg + (orbit.PhaseRateDegPerDay * totalDays);
    }

    /// <summary>
    /// How far from the parent the sibling appears, measured in the orbital plane and signed the way
    /// hour angle runs.
    /// </summary>
    /// <remarks>
    /// The sibling sits at <c>q</c> parent-distances round from the observer and the parent sits one
    /// distance away, so what the observer sees is the difference of those two: a circle of radius
    /// <c>q</c> centred one unit off. When <c>q</c> is under one that circle does not enclose the
    /// observer, and the sibling is penned in about the parent to a greatest elongation of
    /// <c>asin(q)</c> -- Venus's whole behaviour, and the reason an inner sibling cannot cross the
    /// midnight sky. When <c>q</c> is over one the circle does enclose the observer, the elongation
    /// runs right round, and it does so fastest at opposition rather than at a flat rate. A sibling
    /// infinitely far out is the sun: one turn per day, evenly.
    /// </remarks>
    public static double ElongationDeg(double distanceRatio, double phaseDeg)
    {
        var phase = phaseDeg * Math.PI / 180.0;
        var elongation = Math.Atan2(
            distanceRatio * Math.Sin(phase),
            1.0 - (distanceRatio * Math.Cos(phase)));
        return elongation * 180.0 / Math.PI;
    }

    /// <summary>
    /// How far off the body is right now, as a multiple of the parent's distance. One for a body
    /// with no orbit, which puts it level with the parent.
    /// </summary>
    public static double SeparationRatio(NearBodyEntry body, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body.Orbit is { } orbit
            ? SeparationRatio(orbit.DistanceRatio, PhaseDeg(orbit, totalDays))
            : 1.0;
    }

    /// <summary>
    /// The observer-to-sibling distance over the observer-to-parent distance: the third side of the
    /// triangle the two orbits make. Floored well inside any Hill-separated pair, so nothing can
    /// divide by a distance of zero.
    /// </summary>
    public static double SeparationRatio(double distanceRatio, double phaseDeg)
    {
        var phase = phaseDeg * Math.PI / 180.0;
        var squared = 1.0
            + (distanceRatio * distanceRatio)
            - (2.0 * distanceRatio * Math.Cos(phase));
        return Math.Max(Math.Sqrt(Math.Max(squared, 0.0)), MinSeparationRatio);
    }

    /// <summary>
    /// The lit fraction of the disc facing the observer, from the phase angle between the sun and
    /// the body. 1 when the sun is behind the observer, 0 when the body is between the two.
    /// </summary>
    public static double IlluminatedFraction(SkyDirection body, SkyDirection sun)
    {
        var cosPhase = (body.X * sun.X) + (body.Y * sun.Y) + (body.Z * sun.Z);
        return Math.Clamp((1.0 - cosPhase) * 0.5, 0.0, 1.0);
    }
}
