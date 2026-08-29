namespace AstraTerra.Astronomy;

/// <summary>
/// One face a planet can show: the picture to draw, and the phase angle it was drawn at.
/// </summary>
/// <remarks>
/// A planet's face is a picture rather than a shaded model, and the thing a picture cannot fake is
/// phase: Venus is a crescent for half its cycle and no amount of tinting a disc says so. Each
/// planet therefore ships a small set of faces along the phase angle, and the one nearest the angle
/// the planet actually shows is the one drawn.
/// <para>
/// Every face is drawn lit from the left, which is what fixes the quad's orientation: the image's
/// left edge is turned towards the sun, so the crescent points where the sun is rather than wherever
/// the texture happened to be authored.
/// </para>
/// </remarks>
/// <param name="PhaseAngleDeg">
/// Sun-planet-observer angle the face was drawn at: 0 fully lit, 180 a thin crescent.
/// </param>
public sealed record PlanetFaceEntry(double PhaseAngleDeg, string TexturePath);

/// <summary>
/// What a planet looks like once a telescope resolves it into more than a point.
/// </summary>
/// <param name="EquatorialDiameterKm">
/// The globe's own diameter. Together with the distance the ephemeris already computes, this is what
/// makes Mars swell towards opposition and shrink to a speck on the far side of its orbit.
/// </param>
/// <param name="ImageWidthInDiameters">
/// How many globe diameters the whole image spans. One for a bare globe; 2.27 for Saturn, whose
/// rings need the room and whose globe is therefore less than half the picture wide.
/// </param>
/// <param name="Faces">The faces to choose between, by phase angle.</param>
public sealed record PlanetDiscEntry(
    double EquatorialDiameterKm,
    double ImageWidthInDiameters,
    IReadOnlyList<PlanetFaceEntry> Faces
);

/// <summary>
/// A moon of a planet: near enough to its parent that the two are drawn as one sight, and far too
/// small to resolve, so it reads as the point of light a telescope actually shows.
/// </summary>
/// <remarks>
/// Circular orbits, which for these moons is true to a few parts in a thousand and is what the eye
/// at an eyepiece is reading anyway: the moons string out either side of the planet, the innermost
/// swinging back and forth over a night, the outermost over a fortnight.
/// </remarks>
/// <param name="OrbitalPeriodRealDays">
/// The period in real days, the same clock <see cref="WorldEpoch"/> puts the planets on, so a moon
/// keeps its true relationship to its neighbours whatever the world's day length.
/// </param>
/// <param name="MeanLongitudeDeg">Where the moon sits on that orbit at world time zero.</param>
/// <param name="Brightness">How brightly the point draws relative to its parent, 0 to 1.</param>
public sealed record PlanetMoonEntry(
    string Id,
    string DisplayName,
    double SemiMajorAxisKm,
    double OrbitalPeriodRealDays,
    double DiameterKm,
    double MeanLongitudeDeg,
    double Brightness,
    string TexturePath
);

/// <summary>
/// One naked-eye planet: the orbit it is on, and how it should read in the sky.
/// </summary>
/// <param name="Id">Stable key for saves and journals.</param>
/// <param name="AbsoluteMagnitude">
/// Visual magnitude the planet would have at one astronomical unit from both the sun and the
/// observer, fully lit. The zero point of <see cref="PlanetEphemeris.MagnitudeAt"/>.
/// </param>
/// <param name="PhaseCoefficient">
/// Magnitudes lost per degree of phase angle — the sun-planet-observer angle, zero when the planet is
/// fully lit. A single linear term per planet, rather than the published cubics: over the range where
/// a planet is far enough from the sun to be observed at all, the difference is a fraction of a
/// magnitude, and the swing a player notices is dominated by distance.
/// </param>
public sealed record PlanetEntry(
    string Id,
    string DisplayName,
    KeplerianElements Elements,
    double AbsoluteMagnitude,
    double PhaseCoefficient,
    float TintR,
    float TintG,
    float TintB,
    PlanetDiscEntry? Disc = null,
    IReadOnlyList<PlanetMoonEntry>? Moons = null,
    double MoonPlaneTiltDeg = 0.0
);

/// <summary>
/// The shipped planet catalog: the bodies to draw, and the orbit the observer watches them from.
/// </summary>
/// <remarks>
/// Earth is kept separate rather than sitting in the list, because it is not a body to draw — its
/// orbit is subtracted from every other one to turn a heliocentric position into the geocentric one
/// the sky is drawn in.
/// </remarks>
public sealed record PlanetCatalog(
    int SchemaVersion,
    KeplerianElements Observer,
    IReadOnlyList<PlanetEntry> Planets
);
