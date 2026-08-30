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

    // Near the polar circles, sunrise and sunset can both occur inside a 15-minute interval.
    // Five-minute samples still keep this inexpensive while resolving the brief windows that the
    // astrolabe can otherwise misreport as polar night.
    private const double CoarseStepHours = 5.0 / 60.0;
    private const double MotionSampleHours = 0.05;
    private const int RefineIterations = 30;

    public static SkyClockReading Read(
        double totalDays,
        double hoursPerDay,
        Func<double, double> sunAltitudeDegAt)
    {
        ArgumentNullException.ThrowIfNull(sunAltitudeDegAt);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        var localTimeHours = CelestialMath.GetLocalSolarTimeHours(totalDays, hoursPerDay);
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

        for (var step = 1; step <= steps; step++)
        {
            var currentDays = totalDays + (step * stepDays);
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
