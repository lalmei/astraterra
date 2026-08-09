namespace AstraTerra.Astronomy;

/// <param name="SeasonalFactor">Proximity to the peak solar longitude, 1 at maximum and 0 outside the window.</param>
/// <param name="RadiantFactor">The <c>sin(altitude)</c> term, 0 at or below the horizon.</param>
/// <param name="MoonFactor">Moonlight suppression, 1 at new moon.</param>
/// <param name="DarknessFactor">Daylight suppression, 1 under a fully dark sky.</param>
/// <param name="ObservedHourlyRate">Meteors per hour a player should actually see.</param>
public sealed record MeteorShowerReading(
    MeteorShowerEntry Shower,
    double SolarLongitudeDeg,
    double AzimuthDeg,
    double AltitudeDeg,
    double SeasonalFactor,
    double RadiantFactor,
    double MoonFactor,
    double DarknessFactor,
    double ObservedHourlyRate);

/// <summary>
/// Turns a published ZHR into the rate a player standing outside would actually count.
/// </summary>
/// <remarks>
/// Four things reduce it, and a player who watches enough nights learns all four: the world has to
/// be near the peak of its orbit through the debris stream, the radiant has to be high, the moon has
/// to be out of the way, and the sky has to be dark. Sky darkness and moon brightness arrive as
/// plain numbers so this stays testable; the caller resolves them from the game.
/// </remarks>
public static class MeteorShowerActivity
{
    /// <summary>
    /// Orders of magnitude the rate falls by between the peak and the edge of the window. Real
    /// showers decay roughly exponentially in solar longitude rather than tapering off linearly, so
    /// a shower is worth watching for a night or two rather than for its whole activity period.
    /// </summary>
    private const double WindowDecades = 2.0;

    /// <summary>
    /// What is left of the rate under a full moon. Moonlight washes out the faint meteors that make
    /// up most of a shower, but the brightest still get through.
    /// </summary>
    public const double FullMoonRateFactor = 0.2;

    /// <summary>
    /// Darkness at which meteors start to show, matching the gate the starfield renders behind so a
    /// shower never runs in a sky with no stars in it.
    /// </summary>
    public const double MinimumDarkness = 0.10;

    /// <summary>Rate for a shower whose radiant is placed by the world clock and the observer's latitude.</summary>
    public static MeteorShowerReading Read(
        MeteorShowerEntry shower,
        double solarLongitudeDeg,
        double latitudeDeg,
        double localSiderealDeg,
        double skyDarkness,
        double moonBrightness)
    {
        ArgumentNullException.ThrowIfNull(shower);

        var radiant = CelestialMath.GetHorizontalCoordinates(
            shower.RightAscensionDeg,
            shower.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        return Read(shower, solarLongitudeDeg, radiant, skyDarkness, moonBrightness);
    }

    /// <summary>Rate for a shower whose radiant has already been placed in the sky.</summary>
    public static MeteorShowerReading Read(
        MeteorShowerEntry shower,
        double solarLongitudeDeg,
        HorizontalCoordinates radiant,
        double skyDarkness,
        double moonBrightness)
    {
        ArgumentNullException.ThrowIfNull(shower);

        var seasonal = GetSeasonalFactor(shower, solarLongitudeDeg);
        var radiantFactor = GetRadiantFactor(radiant.AltitudeDeg);
        var moon = GetMoonFactor(moonBrightness);
        var darkness = GetDarknessFactor(skyDarkness);
        var rate = Math.Max(0.0, shower.PeakZenithHourlyRate) * seasonal * radiantFactor * moon * darkness;

        return new MeteorShowerReading(
            shower,
            CelestialMath.NormalizeDegrees(solarLongitudeDeg),
            radiant.AzimuthDeg,
            radiant.AltitudeDeg,
            seasonal,
            radiantFactor,
            moon,
            darkness,
            rate);
    }

    /// <summary>Every shower in the catalog, strongest first, so a caller can pick tonight's.</summary>
    public static IReadOnlyList<MeteorShowerReading> ReadAll(
        IEnumerable<MeteorShowerEntry> showers,
        double solarLongitudeDeg,
        double latitudeDeg,
        double localSiderealDeg,
        double skyDarkness,
        double moonBrightness)
    {
        ArgumentNullException.ThrowIfNull(showers);

        return showers
            .Select(shower => Read(shower, solarLongitudeDeg, latitudeDeg, localSiderealDeg, skyDarkness, moonBrightness))
            .OrderByDescending(reading => reading.ObservedHourlyRate)
            .ThenBy(reading => reading.Shower.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// How close the world is to crossing the middle of the debris stream: 1 exactly at the peak
    /// solar longitude, 0 at the window edge and beyond.
    /// </summary>
    /// <remarks>
    /// The separation is measured the short way round the circle, so a window straddling 0/360 deg
    /// behaves exactly like one that does not. Measuring it with a plain subtraction would put a
    /// shower peaking just after the equinox out of action for the whole year bar a few days.
    /// </remarks>
    public static double GetSeasonalFactor(MeteorShowerEntry shower, double solarLongitudeDeg)
    {
        ArgumentNullException.ThrowIfNull(shower);

        var halfWidthDeg = Math.Min(shower.WindowHalfWidthDeg, 180.0);
        if (halfWidthDeg <= 0 || double.IsNaN(solarLongitudeDeg))
        {
            return 0.0;
        }

        var separationDeg = Math.Abs(
            CelestialMath.ShortestAngularDistanceDegrees(shower.PeakSolarLongitudeDeg, solarLongitudeDeg));
        if (separationDeg >= halfWidthDeg)
        {
            return 0.0;
        }

        var edge = Math.Pow(10.0, -WindowDecades);
        var decay = Math.Pow(10.0, -WindowDecades * (separationDeg / halfWidthDeg));
        return (decay - edge) / (1.0 - edge);
    }

    /// <summary>
    /// The share of the stream a flat patch of ground is facing into. Nothing arrives while the
    /// radiant is down, and only a grazing few while it is near the horizon.
    /// </summary>
    public static double GetRadiantFactor(double altitudeDeg)
        => altitudeDeg <= 0 || double.IsNaN(altitudeDeg)
            ? 0.0
            : Math.Sin(Math.Clamp(altitudeDeg, 0.0, 90.0) * Math.PI / 180.0);

    /// <summary>Moonlight suppression: 1 at new moon, <see cref="FullMoonRateFactor"/> at full.</summary>
    public static double GetMoonFactor(double moonBrightness)
    {
        if (double.IsNaN(moonBrightness))
        {
            return 1.0;
        }

        return 1.0 - ((1.0 - FullMoonRateFactor) * Math.Clamp(moonBrightness, 0.0, 1.0));
    }

    /// <summary>
    /// Daylight suppression, from a sky darkness of 0 in full day to 1 at night. Ramps in from
    /// <see cref="MinimumDarkness"/> rather than switching on, so a shower fades up through dusk.
    /// </summary>
    public static double GetDarknessFactor(double skyDarkness)
    {
        if (double.IsNaN(skyDarkness))
        {
            return 0.0;
        }

        var darkness = Math.Clamp(skyDarkness, 0.0, 1.0);
        return Math.Clamp((darkness - MinimumDarkness) / (1.0 - MinimumDarkness), 0.0, 1.0);
    }
}
