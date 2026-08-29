namespace AstraTerra.Astronomy;

/// <summary>
/// Where a planet is, and how bright it is, at a world time.
/// </summary>
/// <remarks>
/// Two orbits are solved for every sample: the planet's and the observer's. Subtracting them is what
/// makes a planet wander — and what makes it turn back on itself for a few weeks when the world
/// overtakes it on the inside. Retrograde motion is not scripted anywhere; it falls out of that
/// subtraction, which is the whole reason this is worth modelling rather than animating.
/// <para>
/// Pure: it takes a world time and returns coordinates, so it can be checked against published
/// positions without a running game. Wrap it in <see cref="CachedSkyEphemeris"/> before sampling it
/// per frame.
/// </para>
/// </remarks>
public sealed class PlanetEphemeris : ISkyEphemeris
{
    private readonly PlanetEntry planet;
    private readonly KeplerianElements observer;
    private readonly int daysPerYear;

    public PlanetEphemeris(PlanetEntry planet, KeplerianElements observer, int daysPerYear)
    {
        ArgumentNullException.ThrowIfNull(planet);
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        this.planet = planet;
        this.observer = observer;
        this.daysPerYear = daysPerYear;
    }

    public PlanetEntry Planet => planet;

    public EquatorialCoordinates PositionAt(double totalDays)
    {
        var geocentric = GetGeocentricPosition(totalDays);
        return CelestialMath.EclipticToEquatorial(geocentric.EclipticLongitudeDeg, geocentric.EclipticLatitudeDeg);
    }

    /// <summary>
    /// Apparent visual magnitude: dimmer with distance from both the sun and the observer, and
    /// dimmer again for the share of the lit face turned away.
    /// </summary>
    /// <remarks>
    /// Worth having rather than a constant per planet, because Venus swings by more than two
    /// magnitudes over a world year as it rounds the far side of its orbit and comes back — a change
    /// a player notices without being told to look for it.
    /// </remarks>
    public double MagnitudeAt(double totalDays)
    {
        var julianCenturies = WorldEpoch.GetJulianCenturies(totalDays, daysPerYear);
        var heliocentric = KeplerianOrbit.GetHeliocentricPosition(planet.Elements, julianCenturies);
        var observerPosition = KeplerianOrbit.GetHeliocentricPosition(observer, julianCenturies);
        var geocentric = heliocentric - observerPosition;

        var sunDistance = heliocentric.Length;
        var observerDistance = geocentric.Length;
        if (sunDistance <= 0 || observerDistance <= 0)
        {
            return planet.AbsoluteMagnitude;
        }

        return planet.AbsoluteMagnitude
            + (5.0 * Math.Log10(sunDistance * observerDistance))
            + (planet.PhaseCoefficient * GetPhaseAngleDeg(sunDistance, observerDistance, observerPosition.Length));
    }

    /// <summary>
    /// The sun-planet-observer angle at a world time: 0 when the planet shows a full face, 180 when
    /// it is a thin crescent between the observer and the sun.
    /// </summary>
    /// <remarks>
    /// Already computed for the magnitude, and needed again by the disc a telescope resolves, which
    /// picks the face it draws by this angle.
    /// </remarks>
    public double PhaseAngleAt(double totalDays)
    {
        var julianCenturies = WorldEpoch.GetJulianCenturies(totalDays, daysPerYear);
        var heliocentric = KeplerianOrbit.GetHeliocentricPosition(planet.Elements, julianCenturies);
        var observerPosition = KeplerianOrbit.GetHeliocentricPosition(observer, julianCenturies);
        var geocentric = heliocentric - observerPosition;
        var sunDistance = heliocentric.Length;
        var observerDistance = geocentric.Length;
        return sunDistance <= 0 || observerDistance <= 0
            ? 0.0
            : GetPhaseAngleDeg(sunDistance, observerDistance, observerPosition.Length);
    }

    /// <summary>The planet as seen from the observer's orbit, in the ecliptic frame.</summary>
    public EclipticVector GetGeocentricPosition(double totalDays)
    {
        var julianCenturies = WorldEpoch.GetJulianCenturies(totalDays, daysPerYear);
        return KeplerianOrbit.GetHeliocentricPosition(planet.Elements, julianCenturies)
            - KeplerianOrbit.GetHeliocentricPosition(observer, julianCenturies);
    }

    /// <summary>
    /// The sun-planet-observer angle, from the triangle the three of them make. Zero when the planet
    /// is fully lit, and largest for an inner planet passing between the observer and the sun.
    /// </summary>
    private static double GetPhaseAngleDeg(double sunDistance, double observerDistance, double sunObserverDistance)
    {
        var cosine = ((sunDistance * sunDistance) + (observerDistance * observerDistance)
            - (sunObserverDistance * sunObserverDistance))
            / (2.0 * sunDistance * observerDistance);
        return Math.Acos(Math.Clamp(cosine, -1.0, 1.0)) * 180.0 / Math.PI;
    }
}
