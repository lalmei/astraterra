namespace AstraTerra.Astronomy;

/// <summary>
/// One point on an authored comet's track across the sky.
/// </summary>
/// <param name="Phase">
/// Where in the apparition this point falls: -1 as the comet is first picked up, 0 at perihelion,
/// +1 as it is lost again. Authoring against the apparition rather than against a day is what keeps
/// a comet's track identical on a 12-day world and a 360-day one.
/// </param>
public sealed record CometPathKeyframe(
    double Phase,
    double RightAscensionDeg,
    double DeclinationDeg
);

/// <summary>
/// One comet: how often it comes back, how long it stays, how bright it gets, and the track it
/// takes while it is here.
/// </summary>
/// <remarks>
/// Authored rather than solved. A near-parabolic orbit could be integrated — Barker's equation
/// handles the eccentricity a real comet has, and <see cref="KeplerianOrbit"/> deliberately does
/// not — but nothing a player experiences would change: the value of a comet is that it is rare,
/// that it can be predicted, and that it visibly builds and fades, and all three are authored
/// numbers either way. What a solved orbit would buy is a track nobody can check against the sky
/// they are standing under. A server owner adding their own comet can write this file; almost
/// nobody can write orbital elements.
/// <para>
/// The track is authored too, so it is a plausible path rather than a real ephemeris. The periods
/// and the parent showers are real.
/// </para>
/// </remarks>
/// <param name="Id">Stable key for saves and journals.</param>
/// <param name="PeriodYears">
/// World years between perihelion passages. World years, not real ones — a world year is a world
/// year however many days it holds, exactly as <see cref="WorldEpoch"/> treats a planet's orbit, so
/// a comet due every thirteen years is due every thirteen years on any world.
/// </param>
/// <param name="FirstPerihelionYear">
/// World years from the start of the world to the first perihelion. Its fractional part places the
/// apparition within the year, so a comet can be authored to arrive in a particular season.
/// </param>
/// <param name="WindowHalfWidthYears">
/// How long either side of perihelion the comet can be seen at all, in world years. A fortnight on
/// a 360-day world is about 0.04.
/// </param>
/// <param name="PeakMagnitude">Visual magnitude at perihelion. Stylised for readability, like the stars.</param>
/// <param name="EdgeMagnitude">Visual magnitude at the very edges of the window, where it is only just there.</param>
/// <param name="BrighteningExponent">
/// How sharply the comet brightens as it closes on perihelion. Below 1 it stays faint until nearly
/// there and then climbs quickly, which is how a real comet behaves; at 1 it brightens evenly across
/// the window. Also shapes the tail, so the two build together.
/// </param>
/// <param name="PeakTailLengthDeg">Degrees of sky the tail spans at perihelion, straight out from the coma.</param>
public sealed record CometEntry(
    string Id,
    string DisplayName,
    double PeriodYears,
    double FirstPerihelionYear,
    double WindowHalfWidthYears,
    double PeakMagnitude,
    double EdgeMagnitude,
    double BrighteningExponent,
    double PeakTailLengthDeg,
    IReadOnlyList<CometPathKeyframe> Path,
    float TintR,
    float TintG,
    float TintB
);

/// <summary>The shipped comet catalog.</summary>
public sealed record CometCatalog(
    int SchemaVersion,
    IReadOnlyList<CometEntry> Comets
);
