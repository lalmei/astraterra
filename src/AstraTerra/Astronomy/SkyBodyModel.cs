namespace AstraTerra.Astronomy;

/// <param name="AzimuthDeg">Clockwise from north, matching <see cref="CelestialMath.GetHorizontalCoordinates"/>.</param>
public sealed record SightedBody(
    string DisplayName,
    double AzimuthDeg,
    double AltitudeDeg,
    double DirectionX,
    double DirectionY,
    double DirectionZ);

/// <summary>
/// Turns sky objects into a single shape the sextant can sight, so the sun and moon are measured
/// the same way stars are.
/// </summary>
public static class SkyBodyModel
{
    private const double DegenerateVectorLength = 1e-9;

    /// <summary>
    /// The altitude the camera is pointed at, from Vintage Story's first-person pitch.
    /// </summary>
    /// <remarks>
    /// Vanilla measures pitch downwards from the zenith and in radians, so <c>pi/2</c> is straight
    /// up, <c>pi</c> is the horizon and <c>3pi/2</c> is straight down — which is why
    /// <see cref="AstraTerra.Observation.SkyLyingPolicy.LookUpPitch"/>, the clamp vanilla applies
    /// just short of straight up, is <c>pi/2 + 0.015</c> rather than something near zero.
    /// <para>
    /// Worth stating in one place because the obvious readings of the raw number are both wrong and
    /// both look plausible in a log: negating it pins every direction to the clamp, and passing it
    /// through unchanged reports the horizon as 180 degrees.
    /// </para>
    /// </remarks>
    public static double GetCameraAltitudeDeg(double pitchRadians)
        => 180.0 - (pitchRadians * 180.0 / Math.PI);

    /// <summary>
    /// Converts a world-space sky direction into a sighting. Vintage Story's sun and moon vectors
    /// live in this space, so they can be passed in directly.
    /// </summary>
    /// <remarks>
    /// This is the exact inverse of the direction built by <c>StarRenderModel</c>: north is -Z,
    /// hence the negated Z in the azimuth.
    /// <para>
    /// Vanilla's sun vector looks at first glance like it references azimuth to +Z, because it
    /// builds Z as <c>sin(zenith) * cos(azimuth)</c> with no negation. It does not: its zenith
    /// angle is <c>2pi - acos(sin(altitude))</c>, which lands in the fourth quadrant where sine is
    /// negative, so that factor supplies the missing minus sign. Vanilla's vectors therefore share
    /// this exact convention and need no rotation.
    /// </para>
    /// The direction components are passed through unchanged so the sextant projects a body to
    /// exactly where it is drawn.
    /// </remarks>
    public static SightedBody? FromWorldDirection(string displayName, double x, double y, double z)
    {
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (length <= DegenerateVectorLength || double.IsNaN(length))
        {
            return null;
        }

        var unitX = x / length;
        var unitY = y / length;
        var unitZ = z / length;
        var altitudeDeg = ToDegrees(Math.Asin(Math.Clamp(unitY, -1.0, 1.0)));
        var azimuthDeg = CelestialMath.NormalizeDegrees(ToDegrees(Math.Atan2(unitX, -unitZ)));

        return new SightedBody(displayName, azimuthDeg, altitudeDeg, x, y, z);
    }

    /// <summary>
    /// Sights anything the sky projection has placed. Naming is the caller's job, because a
    /// catalogue number, a planet and a comet apparition all read differently in the readout.
    /// </summary>
    public static SightedBody FromBody(string displayName, RenderedBody body)
    {
        return new SightedBody(
            displayName,
            body.AzimuthDeg,
            body.AltitudeDeg,
            body.DirectionX,
            body.DirectionY,
            body.DirectionZ);
    }

    public static SightedBody FromStar(RenderedStar star)
        => FromBody($"Star HIP {star.Hip}", star.Body);

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
