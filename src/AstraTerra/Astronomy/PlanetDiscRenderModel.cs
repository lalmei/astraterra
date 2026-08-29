namespace AstraTerra.Astronomy;

/// <summary>
/// A resolved body drawn as a picture on the sky: a planet's disc, or one of its moons.
/// </summary>
/// <param name="ParentId">The planet a moon belongs to; null for the planet itself.</param>
/// <param name="AngularWidthDeg">
/// How wide the whole image draws, rings included — not the globe alone.
/// </param>
/// <param name="QuadCorners">
/// The image's corners as world directions, from its top-left clockwise, matching the winding a
/// deep-sky plate is built with so the two share a mesh builder.
/// </param>
public sealed record RenderedPlanetDisc(
    string Id,
    string DisplayName,
    string? ParentId,
    string TexturePath,
    double AngularWidthDeg,
    double Brightness,
    IReadOnlyList<DeepSkyDirection> QuadCorners
);

/// <summary>
/// Places what a telescope resolves: the planets' discs and the moons of Jupiter and Saturn.
/// </summary>
/// <remarks>
/// <para>
/// A separate pass from <see cref="PlanetRenderModel"/> because it answers a different question. The
/// planet model asks where a point of light goes and how bright it is, which is all the naked eye
/// can report. This one asks how wide the body actually is and which face it is showing, which is
/// only worth computing when something is magnifying it — so it runs only while the scope is raised.
/// </para>
/// <para>
/// Everything here is drawn larger than life by <see cref="DiscExaggeration"/>. That is a deliberate
/// departure and the only one: distances, diameters, orbits and phases are the real ones, and every
/// body is enlarged by the same factor, so the relationships a player can check hold — Mars swells
/// towards opposition, Saturn's globe stays a third the width of its rings, and Callisto stays four
/// times as far out as Io.
/// </para>
/// </remarks>
public sealed class PlanetDiscRenderModel
{
    /// <summary>Kilometres in an astronomical unit, which is what turns an orbit into an angle.</summary>
    public const double KilometresPerAstronomicalUnit = 149_597_870.7;

    /// <summary>
    /// How much larger than life the resolved bodies are drawn.
    /// </summary>
    /// <remarks>
    /// At true scale the finest telescope here shows Jupiter about three pixels across, because a
    /// 16x glass is 16x and Jupiter is forty arcseconds. Chosen so that the widest thing in the
    /// system — Callisto's swing away from Jupiter, about ten arcminutes — still sits inside the
    /// precision telescope's field with room to spare, which is the constraint that decides it: any
    /// larger and the moons a player is trying to count leave the eyepiece.
    /// </remarks>
    public const double DiscExaggeration = 7.5;

    /// <summary>
    /// Smallest a moon may draw. A moon is a point source at any magnification an eyepiece reaches —
    /// enlarged with everything else it would still be under a pixel — so it is held at the size the
    /// stars beside it are drawn, which is what it looks like through a glass.
    /// </summary>
    public const double MinimumMoonWidthDeg = 0.012;

    /// <summary>How much of its parent's brightness a moon keeps at the horizon fade and daylight.</summary>
    private const double MinimumDrawnBrightness = 0.02;

    private readonly IReadOnlyList<(PlanetEntry Planet, PlanetEphemeris Ephemeris)> planets;
    private readonly int daysPerYear;

    public PlanetDiscRenderModel(PlanetCatalog catalog, int daysPerYear)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        this.daysPerYear = daysPerYear;
        planets = catalog.Planets
            .Where(planet => planet.Disc is not null && planet.Disc.Faces.Count > 0)
            .Select(planet => (Planet: planet, Ephemeris: new PlanetEphemeris(planet, catalog.Observer, daysPerYear)))
            .ToList();
    }

    public int Count => planets.Count;

    /// <summary>Every resolved body above the horizon, parents before their moons.</summary>
    public IReadOnlyList<RenderedPlanetDisc> Place(
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        SkyDirection sunDirection)
    {
        var destination = new List<RenderedPlanetDisc>();
        Place(totalDays, latitudeDeg, localSiderealDeg, brightnessBias, sunDirection, destination);
        return destination;
    }

    /// <summary>Places into a list the caller already holds, for the render path.</summary>
    public void Place(
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        SkyDirection sunDirection,
        List<RenderedPlanetDisc> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        foreach (var (planet, ephemeris) in planets)
        {
            PlacePlanet(planet, ephemeris, totalDays, latitudeDeg, localSiderealDeg, brightnessBias, sunDirection, destination);
        }
    }

    private void PlacePlanet(
        PlanetEntry planet,
        PlanetEphemeris ephemeris,
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        SkyDirection sunDirection,
        List<RenderedPlanetDisc> destination)
    {
        var disc = planet.Disc!;
        var geocentric = ephemeris.GetGeocentricPosition(totalDays);
        var distanceKm = geocentric.Length * KilometresPerAstronomicalUnit;
        if (distanceKm <= 0)
        {
            return;
        }

        var body = SkyProjection.Project(
            ephemeris.PositionAt(totalDays),
            ephemeris.MagnitudeAt(totalDays),
            latitudeDeg,
            localSiderealDeg,
            brightnessBias);
        if (body is not { } placed || placed.Brightness < MinimumDrawnBrightness)
        {
            return;
        }

        var direction = new SkyDirection(placed.DirectionX, placed.DirectionY, placed.DirectionZ);

        // Two axes, because the two things the picture has to line up with are different. The face
        // is drawn lit from the left, so the image runs from the sun; the moons run along their own
        // orbital plane, which is the ecliptic to within a couple of degrees. Both are tangents to
        // the sky at the planet, and near opposition the first of them is ill-conditioned -- the sun
        // is behind the observer and has no direction on the sky -- so the ecliptic stands in.
        var eclipticAxis = EclipticTangent(geocentric, direction, latitudeDeg, localSiderealDeg);
        var sunwardAxis = Tangent(direction, sunDirection, fallback: eclipticAxis);

        var globeWidthDeg = ToDegrees(disc.EquatorialDiameterKm / distanceKm) * DiscExaggeration;
        var imageWidthDeg = globeWidthDeg * Math.Max(disc.ImageWidthInDiameters, 0.01);
        destination.Add(new RenderedPlanetDisc(
            planet.Id,
            planet.DisplayName,
            ParentId: null,
            SelectFace(disc.Faces, ephemeris.PhaseAngleAt(totalDays)).TexturePath,
            imageWidthDeg,
            placed.Brightness,
            BuildQuad(direction, Negate(sunwardAxis), imageWidthDeg)));

        if (planet.Moons is null)
        {
            return;
        }

        foreach (var moon in planet.Moons)
        {
            PlaceMoon(planet, moon, direction, eclipticAxis, distanceKm, globeWidthDeg, totalDays, placed.Brightness, destination);
        }
    }

    private void PlaceMoon(
        PlanetEntry planet,
        PlanetMoonEntry moon,
        SkyDirection parentDirection,
        SkyDirection eclipticAxis,
        double distanceKm,
        double parentGlobeWidthDeg,
        double totalDays,
        double parentBrightness,
        List<RenderedPlanetDisc> destination)
    {
        if (moon.OrbitalPeriodRealDays <= 0)
        {
            return;
        }

        var phase = ToRadians(moon.MeanLongitudeDeg)
            + (Math.Tau * RealDaysSinceEpoch(totalDays) / moon.OrbitalPeriodRealDays);
        var elongationDeg = ToDegrees(moon.SemiMajorAxisKm / distanceKm) * DiscExaggeration;
        var tilt = ToRadians(planet.MoonPlaneTiltDeg);

        // The orbit is a circle seen almost edge-on: what reaches the sky is its projection, wide
        // along the orbital plane and squashed across it by however far that plane is tipped. The
        // third component is depth, and its sign is the whole reason to compute it -- a moon on the
        // far side of the swing passes behind the planet rather than in front of it.
        var alongDeg = elongationDeg * Math.Cos(phase);
        var acrossDeg = elongationDeg * Math.Sin(phase) * Math.Sin(tilt);
        var behindParent = Math.Sin(phase) * Math.Cos(tilt) < 0;

        var occulted = behindParent
            && Math.Sqrt((alongDeg * alongDeg) + (acrossDeg * acrossDeg)) < parentGlobeWidthDeg * 0.5;
        if (occulted)
        {
            return;
        }

        var acrossAxis = Cross(eclipticAxis, parentDirection);
        var direction = Normalize(new SkyDirection(
            parentDirection.X + (eclipticAxis.X * ToRadians(alongDeg)) + (acrossAxis.X * ToRadians(acrossDeg)),
            parentDirection.Y + (eclipticAxis.Y * ToRadians(alongDeg)) + (acrossAxis.Y * ToRadians(acrossDeg)),
            parentDirection.Z + (eclipticAxis.Z * ToRadians(alongDeg)) + (acrossAxis.Z * ToRadians(acrossDeg))));

        var widthDeg = Math.Max(
            ToDegrees(moon.DiameterKm / distanceKm) * DiscExaggeration,
            MinimumMoonWidthDeg);
        var brightness = Math.Clamp(parentBrightness * moon.Brightness, 0.0, 1.0);
        if (brightness < MinimumDrawnBrightness)
        {
            return;
        }

        destination.Add(new RenderedPlanetDisc(
            moon.Id,
            moon.DisplayName,
            planet.Id,
            moon.TexturePath,
            widthDeg,
            brightness,
            BuildQuad(direction, eclipticAxis, widthDeg)));
    }

    /// <summary>
    /// Real days past the epoch the moons' periods are quoted in, which is the same clock the
    /// planets keep: one world year is one Julian year, whatever the world's day length.
    /// </summary>
    public double RealDaysSinceEpoch(double totalDays)
        => totalDays / daysPerYear * WorldEpoch.RealDaysPerWorldYear;

    /// <summary>The authored face nearest the phase angle the planet is actually showing.</summary>
    public static PlanetFaceEntry SelectFace(IReadOnlyList<PlanetFaceEntry> faces, double phaseAngleDeg)
    {
        ArgumentNullException.ThrowIfNull(faces);
        if (faces.Count == 0)
        {
            throw new ArgumentException("A planet disc needs at least one face.", nameof(faces));
        }

        var nearest = faces[0];
        var smallestGap = Math.Abs(faces[0].PhaseAngleDeg - phaseAngleDeg);
        for (var index = 1; index < faces.Count; index++)
        {
            var gap = Math.Abs(faces[index].PhaseAngleDeg - phaseAngleDeg);
            if (gap < smallestGap)
            {
                smallestGap = gap;
                nearest = faces[index];
            }
        }

        return nearest;
    }

    /// <summary>
    /// The image's four corners on the sky sphere: a square of <paramref name="widthDeg"/> centred on
    /// the body, with its horizontal running along <paramref name="rightAxis"/>.
    /// </summary>
    /// <remarks>
    /// Bottom-left first, then anticlockwise as the image is seen, because that is the winding
    /// <see cref="AstraTerra.Client.Rendering.DeepSkyQuadMeshBuilder"/> reads: it flips V on the way
    /// out, so the first corner it is handed is the one that takes the picture's bottom row.
    /// </remarks>
    public static IReadOnlyList<DeepSkyDirection> BuildQuad(
        SkyDirection direction,
        SkyDirection rightAxis,
        double widthDeg)
    {
        var up = Cross(rightAxis, direction);
        var half = ToRadians(widthDeg) * 0.5;
        return
        [
            Corner(direction, rightAxis, up, -half, -half),
            Corner(direction, rightAxis, up, half, -half),
            Corner(direction, rightAxis, up, half, half),
            Corner(direction, rightAxis, up, -half, half)
        ];
    }

    private static DeepSkyDirection Corner(
        SkyDirection direction,
        SkyDirection rightAxis,
        SkyDirection up,
        double right,
        double above)
    {
        var corner = Normalize(new SkyDirection(
            direction.X + (rightAxis.X * right) + (up.X * above),
            direction.Y + (rightAxis.Y * right) + (up.Y * above),
            direction.Z + (rightAxis.Z * right) + (up.Z * above)));
        return new DeepSkyDirection(corner.X, corner.Y, corner.Z);
    }

    /// <summary>
    /// The direction of increasing ecliptic longitude at the body, as a tangent to the sky. This is
    /// the line the moons of a planet string out along, because their orbits lie in their parent's
    /// equator and every one of those planes is within a few degrees of the ecliptic.
    /// </summary>
    private static SkyDirection EclipticTangent(
        EclipticVector geocentric,
        SkyDirection direction,
        double latitudeDeg,
        double localSiderealDeg)
    {
        const double stepDeg = 0.05;
        var ahead = CelestialMath.EclipticToEquatorial(
            geocentric.EclipticLongitudeDeg + stepDeg,
            geocentric.EclipticLatitudeDeg);
        var (x, y, z) = SkyProjection.GetWorldDirection(ahead, latitudeDeg, localSiderealDeg);
        return Tangent(direction, new SkyDirection(x, y, z), fallback: AnyPerpendicular(direction));
    }

    /// <summary>
    /// The part of <paramref name="towards"/> that lies on the sky at <paramref name="direction"/>,
    /// which is what a direction in space looks like once it is flattened onto the view.
    /// </summary>
    private static SkyDirection Tangent(SkyDirection direction, SkyDirection towards, SkyDirection fallback)
    {
        var projection = (towards.X * direction.X) + (towards.Y * direction.Y) + (towards.Z * direction.Z);
        var tangent = new SkyDirection(
            towards.X - (direction.X * projection),
            towards.Y - (direction.Y * projection),
            towards.Z - (direction.Z * projection));
        return Length(tangent) < 1e-6 ? fallback : Normalize(tangent);
    }

    /// <summary>Any unit vector at right angles to the direction, for when nothing else fixes one.</summary>
    private static SkyDirection AnyPerpendicular(SkyDirection direction)
    {
        var candidate = Math.Abs(direction.Y) < 0.9
            ? new SkyDirection(0, 1, 0)
            : new SkyDirection(1, 0, 0);
        return Normalize(Cross(direction, candidate));
    }

    private static SkyDirection Cross(SkyDirection left, SkyDirection right)
        => Normalize(new SkyDirection(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)));

    private static SkyDirection Negate(SkyDirection direction)
        => new(-direction.X, -direction.Y, -direction.Z);

    private static double Length(SkyDirection direction)
        => Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));

    private static SkyDirection Normalize(SkyDirection direction)
    {
        var length = Length(direction);
        return length < 1e-12 ? direction : new SkyDirection(direction.X / length, direction.Y / length, direction.Z / length);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
