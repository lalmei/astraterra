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

    public static SightedBody FromStar(RenderedStar star)
        => new(
            $"Star HIP {star.Hip}",
            star.AzimuthDeg,
            star.AltitudeDeg,
            star.DirectionX,
            star.DirectionY,
            star.DirectionZ);

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
