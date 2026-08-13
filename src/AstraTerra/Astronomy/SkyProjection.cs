namespace AstraTerra.Astronomy;

/// <summary>
/// A sky object placed for an observer: where it sits on their horizon, how brightly it reads, and
/// the world direction to draw it along.
/// </summary>
/// <remarks>
/// Deliberately says nothing about <em>what</em> the body is. A catalog number, a colour, a planet's
/// name and phase all belong to the record that wraps this one, so that the projection path stays
/// identical for a star and for something that moves.
/// </remarks>
public sealed record RenderedBody(
    EquatorialCoordinates Coordinates,
    double VisualMagnitude,
    double AzimuthDeg,
    double AltitudeDeg,
    double Brightness,
    double DirectionX,
    double DirectionY,
    double DirectionZ,
    double Size);

/// <summary>
/// Turns an equatorial position into something drawable, for any body that has one.
/// </summary>
/// <remarks>
/// Stars, deep-sky objects, planets and comets differ only in where their right ascension and
/// declination come from — fixed in a catalog, or resolved from an <see cref="ISkyEphemeris"/> at a
/// world time. Everything after that is the same work: hour angle, the horizon cutoff and its fade
/// band, the magnitude curve, and the world direction. It lives here once so a body that moves is
/// drawn by exactly the same arithmetic as the fixed sky it moves against, and so a change to the
/// horizon fade cannot reach the stars while missing the planets.
/// </remarks>
public static class SkyProjection
{
    /// <summary>Altitude at which a body reaches full brightness, fading in below it.</summary>
    public const double DefaultHorizonFadeBandDeg = 10.0;

    /// <summary>
    /// Altitude below which a body is dropped. Deliberately below the mathematical horizon: the
    /// player's eye sits above the terrain, so a star at -1 deg is still worth drawing.
    /// </summary>
    public const double DefaultVisualHorizonCutoffDeg = -15.0;

    /// <summary>
    /// Places a body for an observer, or returns null when it is too far below the horizon to draw.
    /// </summary>
    public static RenderedBody? Project(
        EquatorialCoordinates coordinates,
        double visualMagnitude,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias,
        double horizonFadeBandDeg = DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = DefaultVisualHorizonCutoffDeg)
    {
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            coordinates.RightAscensionDeg,
            coordinates.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        if (horizontal.AltitudeDeg <= visualHorizonCutoffDeg)
        {
            return null;
        }

        var horizonFactor = GetHorizonFadeFactor(horizontal.AltitudeDeg, horizonFadeBandDeg, visualHorizonCutoffDeg);
        var brightness = Math.Clamp(
            GetBrightnessFromMagnitude(visualMagnitude) * brightnessBias * horizonFactor,
            0.0,
            1.0);
        var (directionX, directionY, directionZ) = GetWorldDirection(horizontal.AzimuthDeg, horizontal.AltitudeDeg);

        return new RenderedBody(
            coordinates,
            visualMagnitude,
            horizontal.AzimuthDeg,
            horizontal.AltitudeDeg,
            brightness,
            directionX,
            directionY,
            directionZ,
            GetSizeFromMagnitude(visualMagnitude));
    }

    /// <summary>
    /// How much of its brightness a body keeps this close to the horizon: 1 at and above the fade
    /// band, ramping to 0 at the cutoff, so objects fade out rather than blinking off.
    /// </summary>
    public static double GetHorizonFadeFactor(
        double altitudeDeg,
        double horizonFadeBandDeg = DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = DefaultVisualHorizonCutoffDeg)
    {
        var fadeStart = Math.Min(visualHorizonCutoffDeg, horizonFadeBandDeg - 0.001);
        return Math.Clamp((altitudeDeg - fadeStart) / (horizonFadeBandDeg - fadeStart), 0.0, 1.0);
    }

    /// <summary>
    /// The unit direction to draw along, in world space. North is <c>-Z</c>; see
    /// <c>docs/dev/celestial-model.md</c> for why, and <see cref="SkyBodyModel.FromWorldDirection"/>
    /// for the exact inverse.
    /// </summary>
    public static (double X, double Y, double Z) GetWorldDirection(double azimuthDeg, double altitudeDeg)
    {
        var azimuth = ToRadians(azimuthDeg);
        var altitude = ToRadians(altitudeDeg);
        var horizontal = Math.Cos(altitude);

        return (
            horizontal * Math.Sin(azimuth),
            Math.Sin(altitude),
            -horizontal * Math.Cos(azimuth));
    }

    /// <summary>
    /// The world direction of an equatorial position, for callers that need the direction alone —
    /// a deep-sky quad corner, a comet tail axis — rather than a whole <see cref="RenderedBody"/>.
    /// </summary>
    public static (double X, double Y, double Z) GetWorldDirection(
        EquatorialCoordinates coordinates,
        double latitudeDeg,
        double localSiderealDeg)
    {
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            coordinates.RightAscensionDeg,
            coordinates.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        return GetWorldDirection(horizontal.AzimuthDeg, horizontal.AltitudeDeg);
    }

    /// <summary>
    /// Apparent brightness from visual magnitude, on the mod's deliberately compressed scale: a
    /// naked-eye-limit star keeps a readable floor rather than vanishing. Shared so a planet at
    /// magnitude 1 reads as brightly as a star at magnitude 1.
    /// </summary>
    public static double GetBrightnessFromMagnitude(double visualMagnitude)
    {
        var dimming = Math.Clamp((visualMagnitude - 0.4) / 5.6, 0.0, 1.0);
        return 1.0 - (dimming * 0.64);
    }

    /// <summary>Billboard size in pixels before the renderer's own clamping, from magnitude alone.</summary>
    public static double GetSizeFromMagnitude(double visualMagnitude)
        => Math.Clamp(12.5 * (0.7 + (GetBrightnessFromMagnitude(visualMagnitude) * 0.78)), 7.0, 24.0);

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
