using AstraTerra.Observation;

namespace AstraTerra.Astronomy;

/// <summary>Where the sun crossed the horizon, and which way it was going.</summary>
public sealed record SolarCrossing(SolarEvent Event, double AzimuthDeg, double TotalDays);

/// <summary>
/// Finds the moment the sun's altitude actually reaches zero, and where on the horizon that happens.
/// </summary>
/// <remarks>
/// The mark a disc records is defined by this crossing rather than by the moment somebody clicked.
/// A hill moves apparent sunset by degrees, and a project measured in world months cannot also be a
/// test of reaction time — so the observer may mark anywhere in a window around the crossing and the
/// disc records the crossing itself. Terrain noise and click twitch both leave the measurement.
/// <para>
/// Pure: the caller supplies the sun, so this can be checked without a running game and works on
/// either side of the network.
/// </para>
/// </remarks>
public static class SolarHorizonCrossing
{
    /// <summary>How long either side of the crossing a mark still counts, in world hours.</summary>
    public const double MarkWindowHours = 1.0;

    private const double StepHours = 0.05;

    /// <param name="sunAt">Altitude and azimuth of the sun at a world time, in degrees.</param>
    public static SolarCrossing? FindNearest(
        double totalDays,
        double hoursPerDay,
        Func<double, (double AltitudeDeg, double AzimuthDeg)> sunAt,
        double windowHours = MarkWindowHours)
    {
        ArgumentNullException.ThrowIfNull(sunAt);
        if (hoursPerDay <= 0 || windowHours <= 0)
        {
            return null;
        }

        var window = windowHours / hoursPerDay;
        var step = StepHours / hoursPerDay;
        SolarCrossing? nearest = null;
        var nearestDistance = double.MaxValue;

        for (var at = totalDays - window; at < totalDays + window; at += step)
        {
            var here = sunAt(at).AltitudeDeg;
            var there = sunAt(at + step).AltitudeDeg;
            if (double.IsNaN(here) || double.IsNaN(there) || Math.Sign(here) == Math.Sign(there))
            {
                continue;
            }

            var moment = Refine(at, at + step, sunAt);
            var distance = Math.Abs(moment - totalDays);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = new SolarCrossing(
                there > here ? SolarEvent.Sunrise : SolarEvent.Sunset,
                CelestialMath.NormalizeDegrees(sunAt(moment).AzimuthDeg),
                moment);
            nearestDistance = distance;
        }

        return nearest;
    }

    private static double Refine(double before, double after, Func<double, (double AltitudeDeg, double AzimuthDeg)> sunAt)
    {
        for (var i = 0; i < 30; i++)
        {
            var middle = (before + after) / 2.0;
            if (Math.Sign(sunAt(middle).AltitudeDeg) == Math.Sign(sunAt(before).AltitudeDeg))
            {
                before = middle;
                continue;
            }

            after = middle;
        }

        return (before + after) / 2.0;
    }
}
