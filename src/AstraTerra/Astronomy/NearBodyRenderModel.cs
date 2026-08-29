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
public sealed record PlacedNearBody(
    NearBodyEntry Body,
    SkyDirection Direction,
    SkyDirection SunDirection,
    double AltitudeDeg,
    double AngularDiameterDeg,
    double IlluminatedFraction
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

        // Farthest first, so a sibling moon passing in front of the parent planet is drawn over it.
        placed.Sort(static (a, b) => b.AngularDiameterDeg.CompareTo(a.AngularDiameterDeg));
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

        var rightAscension = RightAscensionDeg(body, totalDays, localSiderealDeg);
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            rightAscension,
            body.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        if (horizontal.AltitudeDeg < HorizonCutoffDeg - (body.AngularDiameterDeg * 0.5))
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
            body.AngularDiameterDeg,
            IlluminatedFraction(direction, sunDirection));
    }

    /// <summary>
    /// Where the body sits among the stars right now. A body with no hour-angle rate stands still
    /// over the ground, so its right ascension has to run with the sidereal clock to stay there.
    /// </summary>
    public static double RightAscensionDeg(NearBodyEntry body, double totalDays, double localSiderealDeg)
    {
        ArgumentNullException.ThrowIfNull(body);
        var hourAngle = body.HourAngleDeg + (body.HourAngleRateDegPerDay * totalDays);
        return CelestialMath.NormalizeDegrees(localSiderealDeg - hourAngle);
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
