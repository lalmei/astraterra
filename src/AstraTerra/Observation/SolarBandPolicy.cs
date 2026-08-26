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
    /// What metal will hold, copper as well as bronze. Metal is where the rim stops getting better:
    /// past copper a disc buys nothing in accuracy, and what it buys instead is room to be engraved.
    /// </summary>
    public const double MetalArcNotchDeg = ArcNotchDeg;

    /// <summary>
    /// What a thumb can press into wet clay and a kiln will keep. Twice bronze's notch, and the disc
    /// a world can build on its first day — before ore, before smelting, before there is any other
    /// way at all to know when the year turns. It finds the same solstice a fortnight less exactly.
    /// </summary>
    public const double RoughArcNotchDeg = 5.0;

    /// <summary>
    /// The closest a disc is ever asked to hold its place. Deliberately the tolerance the astrolabe
    /// treats as still-on-plate: a plate that is wrong can be recut, and a scratch cannot be undone,
    /// so the disc refuses rather than drifts.
    /// </summary>
    public const double MaxLatitudeDriftDeg = AstrolabeCalibrationPolicy.OnPlateToleranceDeg;

    /// <summary>
    /// How far this disc may be carried and still take a mark, which depends on how finely it reads.
    /// </summary>
    /// <remarks>
    /// Moving a degree of latitude moves the setting sun about a degree along the horizon, so a rim
    /// notched at ten degrees cannot see a move of five — and an instrument must not refuse what it
    /// could not have noticed. A rough disc therefore travels further than a fine one, which is the
    /// same bargain as everywhere else on this ladder read backwards: what bronze buys in precision
    /// it pays for in having to be brought home.
    /// </remarks>
    public static double MaxDriftDegFor(double notchDeg)
        => Math.Max(
            MaxLatitudeDriftDeg,
            notchDeg > 0.0 && double.IsFinite(notchDeg) ? notchDeg : ArcNotchDeg);

    /// <summary>
    /// Tilt of the sun's yearly swing, which is what makes the band have edges at all. Shared with
    /// the rest of the sky model so the disc and the astrolabe are describing one world.
    /// </summary>
    public const double ObliquityDeg = CelestialMath.MeanObliquityDeg;

    /// <summary>Puts a sighting on the nearest notch below it, because a scratch has no decimals.</summary>
    public static double Notch(double azimuthDeg, double notchDeg = ArcNotchDeg)
        => InstrumentResolution.Truncate(
            CelestialMath.NormalizeDegrees(azimuthDeg),
            notchDeg > 0.0 && double.IsFinite(notchDeg) ? notchDeg : ArcNotchDeg);

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
