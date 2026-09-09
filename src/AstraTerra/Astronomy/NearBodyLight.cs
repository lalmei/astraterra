namespace AstraTerra.Astronomy;

/// <summary>
/// The one near body big enough to light the ground, reduced to what light needs: a parent giant
/// hanging over a locked moon.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately not a <see cref="NearBodyEntry"/>. An entry carries a painted face, which a
/// dedicated server has no business decoding and no way to draw; light needs four numbers and the
/// server needs them too, because what a giant does to the light is what decides whether things
/// spawn in the open at night. So the drawing catalog and the lighting source travel separately,
/// and only this half crosses to the server.
/// </para>
/// <para>
/// The angular diameter here is the <em>globe</em>, not the whole drawn face. Rings both reflect
/// light and cast shadow, and an edge-on ring seen from inside its own plane does neither in any
/// amount worth modelling, so the ring margin is dropped and the sphere is what lights and what
/// eclipses.
/// </para>
/// </remarks>
/// <param name="AngularDiameterDeg">The globe's width on the sky, rings excluded.</param>
/// <param name="HourAngleDeg">Where it hangs, west of the prime meridian. Fixed: the world is locked to it.</param>
/// <param name="DeclinationDeg">
/// Its angle out of the celestial equator. A regular satellite orbits in its giant's equatorial
/// plane, so from that satellite the giant sits on the observer's own equator and this is near
/// zero -- which is exactly what puts it on the sun's track twice a year.
/// </param>
/// <param name="Albedo">
/// How much of the light falling on it comes back off, 0 to 1. Not the same quantity as
/// <see cref="NearBodyEntry.Brightness"/>, which is how bright to draw the face, though a generated
/// giant's authored brightness is close enough to its bond albedo to be used for both.
/// </param>
public sealed record NearBodyLightSource(
    double AngularDiameterDeg,
    double HourAngleDeg,
    double DeclinationDeg,
    double Albedo
);

/// <summary>
/// What the parent giant is doing to the light right now: how much it is adding to the night, and
/// how much of the sun it is taking away from the day.
/// </summary>
/// <param name="PlanetshineStrength">
/// The giant's own contribution to day-light strength, on Vintage Story's 0-to-1 scale where 1 is
/// full daylight. Compared against the sun's contribution rather than added to it, the same way
/// vanilla compares its moonlight.
/// </param>
/// <param name="SolarObscuration">
/// How much of the sun's disc the giant is covering, 0 clear and 1 total. Only ever non-zero when
/// the two are within a disc-width of each other, which on a locked world is an eclipse season.
/// </param>
public readonly record struct NearBodyIllumination(
    double PlanetshineStrength,
    double SolarObscuration
)
{
    public static NearBodyIllumination None { get; } = new(0.0, 0.0);
}

/// <summary>
/// How a parent giant lights the moon beneath it, and how it blocks the sun from it.
/// </summary>
/// <remarks>
/// <para>
/// A tidally locked moon is not a world with a bright moon of its own; it is a world with a second
/// source of daylight. The giant is tens of degrees wide, it is full at local midnight -- because
/// midnight is exactly when the observer is on the far side from the sun and the giant's whole lit
/// face is turned their way -- and it never sets. Io's nights under a full Jupiter run a couple of
/// hundred times brighter than a full moon on Earth. Leaving that out does not make the model
/// simpler, it makes the night wrong.
/// </para>
/// <para>
/// The other half is the same geometry read the other way. If the giant is on the observer's
/// celestial equator -- which it is, because a regular satellite orbits in its giant's equatorial
/// plane -- then the sun crosses its hour angle once every day, and whether that crossing is an
/// eclipse depends only on where the sun's declination has got to in the year. Twice a year the
/// two line up and the giant walks across the sun; the rest of the year it passes above or below.
/// That is an eclipse season, and it is the honest reason a locked moon does not go dark daily.
/// </para>
/// <para>
/// Nothing here reads the game. It is given directions and returns numbers, so the seasons can be
/// counted in a test rather than waited for in a world.
/// </para>
/// </remarks>
public static class NearBodyLight
{
    /// <summary>
    /// What Vintage Story's own full moon puts into day-light strength, from the top of its
    /// <c>MoonBrightnesByPhase</c> table.
    /// </summary>
    /// <remarks>
    /// This is the anchor the whole scale hangs off. It is not a physical number -- a real full moon
    /// is about four parts in a million of daylight, not a third of it -- but the game's light scale
    /// is perceptual, and matching the game matters more here than matching the sky. Planetshine is
    /// therefore expressed as "this many times a vanilla full moon" and mapped onto the band vanilla
    /// already uses, so a giant reads as brighter than a full moon by an amount a player can feel
    /// without the night turning into an afternoon.
    /// </remarks>
    public const double VanillaFullMoonLight = 0.33;

    /// <summary>
    /// Reflected flux from Earth's full moon as a fraction of Earth's own noon, which is what a
    /// flux ratio is measured against.
    /// </summary>
    /// <remarks>
    /// <c>albedo * sin^2(angularRadius) * 2/3</c> for a 0.52-degree disc at 0.12 albedo. The
    /// two-thirds is the Lambert sphere's phase integral at full. It comes out at 1.6e-6, and the
    /// real ratio -- 0.25 lux against roughly 100,000 -- is 2.5e-6, which is as close as a
    /// one-line reflectance model has any right to be.
    /// </remarks>
    public const double FullMoonFluxRatio = 1.63e-6;

    /// <summary>
    /// How much light a tenfold rise in reflected flux is worth, in day-light strength.
    /// </summary>
    /// <remarks>
    /// The compression is the point. Between a full moon and full daylight lies a factor of some
    /// hundreds of thousands, and Vintage Story spends only 0.33 to 1.0 on it, so light in this
    /// game is already roughly logarithmic in flux. This coefficient continues that curve upward
    /// from the full-moon anchor: a giant like Jupiter seen from Io, about four thousand full
    /// moons, lands near half of full daylight -- an unmistakably lit night that is still plainly
    /// not a day.
    /// </remarks>
    public const double LightPerFluxDecade = 0.037;

    /// <summary>
    /// The most planetshine may contribute, however large and bright the giant.
    /// </summary>
    /// <remarks>
    /// This is a guard against nonsense input, not a shaper of ordinary worlds, and the difference
    /// is worth being clear about: a logarithm is gentle, and even a giant filling ninety degrees
    /// of sky and reflecting every photon that reaches it only gets to about 0.53. No generated
    /// world can reach this ceiling. What it catches is an albedo above one or a diameter that
    /// arithmetic elsewhere has gone wrong on, and what it guarantees is that no such value can
    /// make a night brighter than a heavily overcast day.
    /// </remarks>
    public const double MaxPlanetshineLight = 0.75;

    /// <summary>
    /// How far above the horizon the giant has to be for its light to count in full.
    /// </summary>
    /// <remarks>
    /// Light arriving nearly along the ground both spreads over more of it and comes through more
    /// air, so a giant low in the sky lights less than one overhead. This is the same slow fade
    /// vanilla applies to its moon, which uses the moon's own height directly; the width of the
    /// band is what stops a giant whose centre is at the horizon -- and which may be twenty degrees
    /// wide, so half of it is still up -- from switching off at once.
    /// </remarks>
    public const double AltitudeFadeBandDeg = 12.0;

    /// <summary>
    /// The sun's own width on the sky. Not generated: every world here is at an Earth-like
    /// insolation, so its sun subtends close enough to half a degree that authoring one would be
    /// precision the rest of the model cannot pay for.
    /// </summary>
    public const double SunAngularDiameterDeg = 0.53;

    /// <summary>
    /// What the giant is doing to the light, from where it and the sun are standing.
    /// </summary>
    /// <param name="source">The giant, or null on a world that has none.</param>
    /// <param name="giantDirection">Unit direction to the giant, in the same frame as the sun.</param>
    /// <param name="giantAltitudeDeg">Its centre's height above the horizon.</param>
    /// <param name="sunDirection">Unit direction to the sun.</param>
    public static NearBodyIllumination Illumination(
        NearBodyLightSource? source,
        SkyDirection giantDirection,
        double giantAltitudeDeg,
        SkyDirection sunDirection)
    {
        if (source is null || source.AngularDiameterDeg <= 0.0)
        {
            return NearBodyIllumination.None;
        }

        var separationDeg = SeparationDeg(giantDirection, sunDirection);
        var obscuration = Obscuration(separationDeg, source.AngularDiameterDeg, SunAngularDiameterDeg);

        // The giant's own night is the observer's too. When the moon is inside the giant's shadow
        // the giant is between the observer and the sun -- which is the same alignment as the
        // eclipse -- so the lit fraction is already near zero there and no separate shadow term is
        // needed: phase does the work, as it does for every other body in this sky.
        var illuminatedFraction = IlluminatedFraction(separationDeg);
        var flux = source.Albedo
            * SinSquared(source.AngularDiameterDeg * 0.5)
            * (2.0 / 3.0)
            * illuminatedFraction;

        return new NearBodyIllumination(
            PlanetshineStrength(flux) * AltitudeFade(giantAltitudeDeg, source.AngularDiameterDeg),
            obscuration);
    }

    /// <summary>
    /// Day-light strength from a reflected flux, as a fraction of the observer's own noon.
    /// </summary>
    /// <remarks>
    /// Anchored at Vintage Story's full moon and continued logarithmically from there, so a source
    /// dimmer than a full moon is worth proportionally less and a brighter one climbs by decades
    /// rather than by multiples. Zero flux is zero light, not minus infinity.
    /// </remarks>
    public static double PlanetshineStrength(double fluxRatio)
    {
        if (fluxRatio <= 0.0 || !double.IsFinite(fluxRatio))
        {
            return 0.0;
        }

        var moons = fluxRatio / FullMoonFluxRatio;
        var light = moons <= 1.0
            ? VanillaFullMoonLight * moons
            : VanillaFullMoonLight + (LightPerFluxDecade * Math.Log10(moons));
        return Math.Clamp(light, 0.0, MaxPlanetshineLight);
    }

    /// <summary>
    /// How much of the sun's disc a body of <paramref name="bodyDiameterDeg"/> covers when their
    /// centres are <paramref name="separationDeg"/> apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The eclipsing body here is tens of degrees wide and the sun is half a degree, so the partial
    /// phase is not the lens-shaped overlap of two comparable discs -- it is a straight edge
    /// sweeping across the sun. That makes the covered fraction a circular segment of the sun
    /// alone, which is exact for this size ratio and cheap, and it is what gives the long partial
    /// shoulder either side of totality that an eclipse season should have.
    /// </para>
    /// <para>
    /// Totality is genuine and total: nothing here is an annular eclipse, because a body forty
    /// times the sun's width cannot leave a ring.
    /// </para>
    /// </remarks>
    public static double Obscuration(double separationDeg, double bodyDiameterDeg, double sunDiameterDeg)
    {
        var bodyRadius = bodyDiameterDeg * 0.5;
        var sunRadius = sunDiameterDeg * 0.5;
        if (bodyRadius <= 0.0 || sunRadius <= 0.0)
        {
            return 0.0;
        }

        if (separationDeg <= bodyRadius - sunRadius)
        {
            return 1.0;
        }

        if (separationDeg >= bodyRadius + sunRadius)
        {
            return 0.0;
        }

        // How far the eclipsing edge has cut past the sun's centre, as a fraction of the sun's
        // radius: -1 as the edge first touches, +1 as it clears the far limb.
        var cut = Math.Clamp((bodyRadius - separationDeg) / sunRadius, -1.0, 1.0);
        var angle = Math.Acos(-cut);
        return Math.Clamp((angle - (Math.Sin(angle) * Math.Cos(angle))) / Math.PI, 0.0, 1.0);
    }

    /// <summary>The lit fraction of a disc whose centre is this far from the sun's on the sky.</summary>
    /// <remarks>
    /// The same quantity <see cref="NearBodyRenderModel.IlluminatedFraction"/> works out from
    /// vectors, restated on an angle because that is what the eclipse geometry already has in hand.
    /// Zero at new, when the body is in front of the sun; one at full, when the sun is behind the
    /// observer's back.
    /// </remarks>
    public static double IlluminatedFraction(double separationDeg)
        => Math.Clamp((1.0 - Math.Cos(separationDeg * Math.PI / 180.0)) * 0.5, 0.0, 1.0);

    /// <summary>Angle between two unit directions, in degrees.</summary>
    public static double SeparationDeg(SkyDirection a, SkyDirection b)
    {
        var dot = Math.Clamp((a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z), -1.0, 1.0);
        return Math.Acos(dot) * 180.0 / Math.PI;
    }

    /// <summary>
    /// How much of the giant's light survives its height above the horizon, 1 well up and 0 down.
    /// </summary>
    /// <remarks>
    /// Measured from the disc's lower limb rather than its centre, so a wide giant keeps lighting
    /// the ground while most of it is still up. A locked world's giant does not actually rise or
    /// set, but it can be authored low, and an observer far enough toward the far side of the world
    /// never sees it at all -- which is a real place on a real moon, and should be a dark one.
    /// </remarks>
    public static double AltitudeFade(double altitudeDeg, double angularDiameterDeg)
    {
        var lowerLimbDeg = altitudeDeg + (angularDiameterDeg * 0.5);
        return Math.Clamp(lowerLimbDeg / AltitudeFadeBandDeg, 0.0, 1.0);
    }

    private static double SinSquared(double degrees)
    {
        var sin = Math.Sin(degrees * Math.PI / 180.0);
        return sin * sin;
    }
}
