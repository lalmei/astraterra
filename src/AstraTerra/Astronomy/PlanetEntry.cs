namespace AstraTerra.Astronomy;

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
    float TintB
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
