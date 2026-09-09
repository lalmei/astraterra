namespace AstraTerra.Astronomy;

public enum SkyPhase
{
    Day,
    Dusk,
    Night,
    Dawn
}

/// <param name="LocalTimeHours">Hour of the world day, 0 to <c>hoursPerDay</c>.</param>
/// <param name="HoursUntilSunrise">Null when the sun does not rise within a day, as at a polar winter.</param>
/// <param name="HoursUntilSunset">Null when the sun does not set within a day, as at a polar summer.</param>
public sealed record SkyClockReading(
    double LocalTimeHours,
    SkyPhase Phase,
    double SunAltitudeDeg,
    double? HoursUntilSunrise,
    double? HoursUntilSunset);

/// <summary>
/// Tells the time of night the way a nocturnal does: from where the sun actually is, rather than
/// from a fixed sunrise hour. The caller supplies the sun altitude so this stays pure and testable;
/// the HUD feeds it Vintage Story's own sun position, which is what drives the visible daylight.
/// </summary>
public static class SkyClock
{
    /// <summary>Civil twilight. Above this the sky still reads as dusk or dawn rather than night.</summary>
    public const double CivilTwilightAltitudeDeg = -6.0;

    /// <summary>
    /// How far apart the samples of the first pass are, in hours.
    /// </summary>
    /// <remarks>
    /// A grid alone can never be fine enough. The daylight window shrinks continuously to zero as an
    /// observer approaches the polar circle at the solstice, so whatever this is, there are days on
    /// either side of that transition where the whole window falls between two samples and the
    /// instrument reports a polar night on a day the sun does rise. The sweep therefore checks
    /// itself -- see <see cref="FindNextHorizonCrossing"/> -- and once it does, this only has to be
    /// fine enough to find the day's shape, not its shortest event. A quarter of an hour costs a
    /// quarter of the sun evaluations that five minutes did.
    /// <para>
    /// Which latitude that transition sits at is not fixed either. The polar circle is at ninety
    /// degrees less the world's axial tilt, and on a generated moon world that tilt is its parent
    /// giant's -- so the band this has to get right can be at sixty-six degrees, or at forty-five,
    /// or at eighty-seven, depending on the world.
    /// </para>
    /// </remarks>
    private const double CoarseStepHours = 15.0 / 60.0;

    /// <summary>
    /// How finely the one suspicious interval is re-scanned when the first pass finds nothing.
    /// </summary>
    /// <remarks>
    /// Sixty-four samples across two coarse steps resolves a window of about fourteen seconds. Well
    /// past what a player can act on, and it is paid only on the days that need it -- a polar night
    /// or a polar day, where the first pass returned nothing at all.
    /// </remarks>
    private const int RescanSamples = 64;
    private const double MotionSampleHours = 0.05;
    private const int RefineIterations = 30;

    public static SkyClockReading Read(
        double totalDays,
        double hoursPerDay,
        Func<double, double> sunAltitudeDegAt,
        double longitudeDegrees = 0)
    {
        ArgumentNullException.ThrowIfNull(sunAltitudeDegAt);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        var localTimeHours = CelestialMath.GetLocalSolarTimeHours(
            totalDays,
            hoursPerDay,
            longitudeDegrees);
        var sunAltitudeDeg = sunAltitudeDegAt(totalDays);
        var hoursUntilSunrise = FindNextHorizonCrossing(totalDays, hoursPerDay, sunAltitudeDegAt, rising: true);
        var hoursUntilSunset = FindNextHorizonCrossing(totalDays, hoursPerDay, sunAltitudeDegAt, rising: false);

        return new SkyClockReading(
            localTimeHours,
            ClassifyPhase(totalDays, hoursPerDay, sunAltitudeDeg, sunAltitudeDegAt),
            sunAltitudeDeg,
            hoursUntilSunrise,
            hoursUntilSunset);
    }

    private static SkyPhase ClassifyPhase(
        double totalDays,
        double hoursPerDay,
        double sunAltitudeDeg,
        Func<double, double> sunAltitudeDegAt)
    {
        if (sunAltitudeDeg > 0)
        {
            return SkyPhase.Day;
        }

        if (sunAltitudeDeg <= CivilTwilightAltitudeDeg)
        {
            return SkyPhase.Night;
        }

        var climbing = sunAltitudeDegAt(totalDays + (MotionSampleHours / hoursPerDay)) > sunAltitudeDeg;
        return climbing ? SkyPhase.Dawn : SkyPhase.Dusk;
    }

    /// <summary>
    /// Walks forward at most one world day looking for the sun crossing the horizon in the wanted
    /// direction, then bisects the bracketing interval. Returns null at a polar day or night, where
    /// no crossing exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two samples that straddle the horizon prove a crossing. Two samples on the same side prove
    /// nothing -- the sun can rise and set again between them -- and that is the case this has to
    /// survive, because near the polar circle at the solstice the entire day is shorter than any
    /// grid a caller can afford. So when the first pass finds no crossing at all, the sweep does not
    /// conclude polar night; it asks where the sun came closest to the horizon and looks there
    /// properly.
    /// </para>
    /// <para>
    /// One re-scan is enough because the sun's altitude has a single maximum and a single minimum
    /// over a day. If every sample was below the horizon, any hidden daylight is around the highest
    /// of them; if every sample was above it, any hidden night is around the lowest. The true
    /// extreme lies within one step of that sample, so a bracket of one step either side contains
    /// the whole event, whichever side of it the caller happens to want.
    /// </para>
    /// </remarks>
    private static double? FindNextHorizonCrossing(
        double totalDays,
        double hoursPerDay,
        Func<double, double> sunAltitudeDegAt,
        bool rising)
    {
        var stepDays = CoarseStepHours / hoursPerDay;
        var steps = (int)Math.Ceiling(hoursPerDay / CoarseStepHours);
        var previousDays = totalDays;
        var previousAltitude = sunAltitudeDegAt(totalDays);

        var anyAbove = previousAltitude >= 0.0;
        var highestDays = totalDays;
        var highestAltitude = previousAltitude;
        var lowestDays = totalDays;
        var lowestAltitude = previousAltitude;

        for (var step = 1; step <= steps; step++)
        {
            var currentDays = totalDays + (step * stepDays);
            var currentAltitude = sunAltitudeDegAt(currentDays);
            if (HasCrossed(previousAltitude, currentAltitude, rising))
            {
                var crossingDays = Refine(previousDays, currentDays, sunAltitudeDegAt, rising);
                return Math.Max(0.0, (crossingDays - totalDays) * hoursPerDay);
            }

            anyAbove |= currentAltitude >= 0.0;
            if (currentAltitude > highestAltitude)
            {
                highestAltitude = currentAltitude;
                highestDays = currentDays;
            }

            if (currentAltitude < lowestAltitude)
            {
                lowestAltitude = currentAltitude;
                lowestDays = currentDays;
            }

            previousDays = currentDays;
            previousAltitude = currentAltitude;
        }

        // Every sample landed on one side of the horizon. The event, if there is one, is hidden
        // around the extreme that reaches toward the other side.
        var suspectDays = anyAbove ? lowestDays : highestDays;
        return Rescan(
            totalDays,
            hoursPerDay,
            sunAltitudeDegAt,
            rising,
            Math.Max(totalDays, suspectDays - stepDays),
            Math.Min(totalDays + (steps * stepDays), suspectDays + stepDays));
    }

    /// <summary>
    /// Looks for a crossing inside one suspicious interval, at a resolution the first pass cannot
    /// afford over a whole day.
    /// </summary>
    private static double? Rescan(
        double totalDays,
        double hoursPerDay,
        Func<double, double> sunAltitudeDegAt,
        bool rising,
        double fromDays,
        double toDays)
    {
        if (toDays <= fromDays)
        {
            return null;
        }

        var stepDays = (toDays - fromDays) / RescanSamples;
        var previousDays = fromDays;
        var previousAltitude = sunAltitudeDegAt(fromDays);

        for (var step = 1; step <= RescanSamples; step++)
        {
            var currentDays = fromDays + (step * stepDays);
            var currentAltitude = sunAltitudeDegAt(currentDays);
            if (HasCrossed(previousAltitude, currentAltitude, rising))
            {
                var crossingDays = Refine(previousDays, currentDays, sunAltitudeDegAt, rising);
                return Math.Max(0.0, (crossingDays - totalDays) * hoursPerDay);
            }

            previousDays = currentDays;
            previousAltitude = currentAltitude;
        }

        return null;
    }

    private static bool HasCrossed(double previousAltitude, double currentAltitude, bool rising)
        => rising
            ? previousAltitude < 0 && currentAltitude >= 0
            : previousAltitude > 0 && currentAltitude <= 0;

    private static double Refine(
        double lowDays,
        double highDays,
        Func<double, double> sunAltitudeDegAt,
        bool rising)
    {
        for (var iteration = 0; iteration < RefineIterations; iteration++)
        {
            var midDays = (lowDays + highDays) / 2.0;
            var midAltitude = sunAltitudeDegAt(midDays);
            var belowHorizon = rising ? midAltitude < 0 : midAltitude > 0;
            if (belowHorizon)
            {
                lowDays = midDays;
            }
            else
            {
                highDays = midDays;
            }
        }

        return highDays;
    }
}
