using AstraTerra.Astronomy;

namespace AstraTerra.Observation;

/// <summary>Which end of the day a mark was scratched at. Both arcs sit on the same disc.</summary>
public enum SolarEvent
{
    Sunrise = 0,
    Sunset = 1
}

/// <summary>
/// The rules the bronze disc keeps: how coarse a notch is, how far its owner may wander, and what
/// the width of a finished band means.
/// </summary>
/// <remarks>
/// Nothing here reads an angle. The disc is scratched, not graduated: a mark is a notch on a rim,
/// and the instrument's whole output is where the notches stop. That it yields a latitude at all is
/// a consequence of geometry rather than of precision, which is why bronze can do it.
/// </remarks>
public static class SolarBandPolicy
{
    /// <summary>
    /// How wide a notch is. The real disc carries thirty-odd marks around an arc of about eighty
    /// degrees, so this is the scale that artefact actually works at — and it is coarse enough that
    /// the days around a turning point land on the same notch, which is the point.
    /// </summary>
    public const double ArcNotchDeg = 2.5;

    /// <summary>
    /// How far an observer may carry the disc and still add to its band. Deliberately the tolerance
    /// the astrolabe treats as still-on-plate: a plate that is wrong can be recut, and a scratch in
    /// bronze cannot, so the disc refuses rather than drifts.
    /// </summary>
    public const double MaxLatitudeDriftDeg = AstrolabeCalibrationPolicy.OnPlateToleranceDeg;

    /// <summary>
    /// Tilt of the sun's yearly swing, which is what makes the band have edges at all. Shared with
    /// the rest of the sky model so the disc and the astrolabe are describing one world.
    /// </summary>
    public const double ObliquityDeg = CelestialMath.MeanObliquityDeg;

    /// <summary>Puts a sighting on the nearest notch below it, because a scratch has no decimals.</summary>
    public static double Notch(double azimuthDeg)
        => InstrumentResolution.Truncate(CelestialMath.NormalizeDegrees(azimuthDeg), ArcNotchDeg);

    /// <summary>
    /// How wide the band of sunsets is at a latitude: the angle between midsummer's setting point
    /// and midwinter's.
    /// </summary>
    /// <remarks>
    /// From the sunset condition alone — the sun's altitude reaching zero — the setting azimuth is
    /// <c>acos(sin δ / cos φ)</c>, so the two solstice declinations bracket the whole year's worth of
    /// sunsets. Everything the disc pays out follows from that one relation.
    /// </remarks>
    public static double? SwingDegAt(double latitudeDeg)
    {
        var ratio = -Math.Sin(ToRadians(ObliquityDeg)) / Math.Cos(ToRadians(latitudeDeg));
        if (Math.Abs(ratio) > 1.0)
        {
            // Inside the polar circles the sun stops setting for part of the year, and a band of
            // sunsets is not a thing that exists there.
            return null;
        }

        return 2.0 * (ToDegrees(Math.Acos(ratio)) - 90.0);
    }

    /// <summary>
    /// The latitude a finished band implies. The inverse of <see cref="SwingDegAt"/>, and the reason
    /// a year of patience buys what the astrolabe buys in a three-second sighting.
    /// </summary>
    public static double? LatitudeFromSwingDeg(double swingDeg)
    {
        if (swingDeg <= 0.0 || swingDeg >= 360.0)
        {
            return null;
        }

        var cosine = Math.Sin(ToRadians(ObliquityDeg)) / Math.Sin(ToRadians(swingDeg / 2.0));
        return Math.Abs(cosine) > 1.0 ? null : ToDegrees(Math.Acos(cosine));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
