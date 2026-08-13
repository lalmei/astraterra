namespace AstraTerra.Astronomy;

/// <summary>
/// A planet placed in the sky: the same <see cref="RenderedBody"/> a star is placed as, plus the
/// things that make it read as a planet rather than a star.
/// </summary>
public sealed record RenderedPlanet(
    string Id,
    string DisplayName,
    RenderedBody Body,
    double Brilliance,
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
}

/// <summary>
/// Places the naked-eye planets for an observer, sampling each orbit no more often than it can
/// visibly move.
/// </summary>
/// <remarks>
/// Holds one <see cref="CachedSkyEphemeris"/> per planet, which is why this has instances where the
/// star and deep-sky models are static: a planet's position is a function of world time and costs
/// real arithmetic, so it is worth remembering between frames. Projection itself still runs per
/// frame, because that is what turns with the sky.
/// </remarks>
public sealed class PlanetRenderModel
{
    /// <summary>Magnitude at which a planet stops standing out from the star field at all.</summary>
    public const double FaintestBrillianceMagnitude = 2.0;

    /// <summary>Magnitude at which a planet is as commanding as the sky ever gets: Venus at its best.</summary>
    public const double BrightestBrillianceMagnitude = -4.5;

    private readonly IReadOnlyList<(PlanetEntry Planet, ISkyEphemeris Ephemeris)> planets;

    public PlanetRenderModel(PlanetCatalog catalog, int daysPerYear, double hoursPerDay)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        planets = catalog.Planets
            .Select(planet => (
                Planet: planet,
                Ephemeris: (ISkyEphemeris)new CachedSkyEphemeris(
                    new PlanetEphemeris(planet, catalog.Observer, daysPerYear),
                    hoursPerDay)))
            .ToList();
    }

    public int Count => planets.Count;

    /// <summary>Every planet currently above the drawing horizon, brightest first.</summary>
    public IReadOnlyList<RenderedPlanet> ProjectVisiblePlanets(
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        return planets
            .Select(entry => Project(
                entry.Planet,
                entry.Ephemeris,
                totalDays,
                latitudeDeg,
                localSiderealDeg,
                brightnessBias,
                horizonFadeBandDeg,
                visualHorizonCutoffDeg))
            .Where(planet => planet is not null)
            .Select(planet => planet!)
            .OrderByDescending(planet => planet.Brightness)
            .ThenBy(planet => planet.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Resolves a planet's position and magnitude for the world time, then hands both to the same
    /// projection a star goes through.
    /// </summary>
    public static RenderedPlanet? Project(
        PlanetEntry planet,
        ISkyEphemeris ephemeris,
        double totalDays,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = SkyProjection.DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = SkyProjection.DefaultVisualHorizonCutoffDeg)
    {
        ArgumentNullException.ThrowIfNull(planet);
        ArgumentNullException.ThrowIfNull(ephemeris);

        var body = SkyProjection.Project(
            ephemeris.PositionAt(totalDays),
            ephemeris.MagnitudeAt(totalDays),
            latitudeDeg,
            localSiderealDeg,
            brightnessBias,
            horizonFadeBandDeg,
            visualHorizonCutoffDeg);

        return body is null
            ? null
            : new RenderedPlanet(
                planet.Id,
                planet.DisplayName,
                body,
                GetBrilliance(body.VisualMagnitude),
                planet.TintR,
                planet.TintG,
                planet.TintB);
    }

    /// <summary>
    /// How much a planet should outshine the star field, from 0 at the faint end to 1 for Venus at
    /// its most brilliant.
    /// </summary>
    /// <remarks>
    /// The shared magnitude curve is deliberately compressed for stars and saturates at magnitude
    /// 0.4, which every planet but Saturn spends most of its time brighter than. Left at that, Venus
    /// at -4.9 and Mars at its dimmest would be drawn identically, and the swing that makes a planet
    /// worth watching would never reach the screen. This keeps responding across the range planets
    /// actually occupy, and the renderer spends it on the glow rather than on the core, so a planet
    /// stays a point of light rather than growing into a disc.
    /// </remarks>
    public static double GetBrilliance(double visualMagnitude)
        => Math.Clamp(
            (FaintestBrillianceMagnitude - visualMagnitude)
                / (FaintestBrillianceMagnitude - BrightestBrillianceMagnitude),
            0.0,
            1.0);
}
