using AstraTerra.Astronomy;
using Vintagestory.API.Common;

namespace AstraTerra.Config;

public static class ClockDisplay
{
    private static readonly System.Func<double, IWorldAccessor, double>? CanonicalLongitudeMapper =
        typeof(LatitudeMapper)
            .GetMethod(
                nameof(LatitudeMapper.MapWorldLongitude),
                [typeof(double), typeof(IWorldAccessor)])
            ?.CreateDelegate<System.Func<double, IWorldAccessor, double>>();

    /// <summary>
    /// Observer longitude on the same climate scale used by #44. Kept self-contained so this PR is
    /// valid against main; once #44 lands callers can use LatitudeMapper's world overload directly.
    /// </summary>
    public static double MapWorldLongitude(double x, IWorldAccessor world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (CanonicalLongitudeMapper is not null)
        {
            return CanonicalLongitudeMapper(x, world);
        }

        const double defaultPolarEquatorDistance = 50000.0;
        var configured = world.Config?.GetInt("polarEquatorDistance", -1) ?? -1;
        var polarEquatorDistance = configured > 0 ? configured : defaultPolarEquatorDistance;
        if (configured <= 0)
        {
            var configuredText = world.Config?.GetString("polarEquatorDistance", null);
            if (configuredText is not null
                && int.TryParse(configuredText, out var parsed)
                && parsed > 0)
            {
                polarEquatorDistance = parsed;
            }
        }

        var mapSizeX = world.BlockAccessor.MapSizeX;
        if (mapSizeX <= 0 || polarEquatorDistance <= 0)
        {
            return 0;
        }

        var circumference = polarEquatorDistance * 4.0;
        var delta = PositiveModulo(x - mapSizeX * 0.5, circumference);
        if (delta > circumference * 0.5)
        {
            delta -= circumference;
        }

        var degrees = delta / polarEquatorDistance * 90.0;
        var wrapped = degrees % 360.0;
        if (wrapped <= -180.0)
        {
            return wrapped + 360.0;
        }

        return wrapped > 180.0 ? wrapped - 360.0 : wrapped;
    }

    public static double GetDisplayedSolarTimeHours(
        double totalDays,
        double hoursPerDay,
        double longitudeDegrees,
        DisplayedClockTime display)
    {
        var universal = CelestialMath.GetUniversalSolarTimeHours(totalDays, hoursPerDay);
        return display switch
        {
            DisplayedClockTime.Universal => universal,
            DisplayedClockTime.LocalSolar => CelestialMath.ApplyLongitudeToSolarHours(
                universal,
                longitudeDegrees,
                hoursPerDay),
            DisplayedClockTime.LocalTimeZone => CelestialMath.ApplyTimeZoneToSolarHours(
                universal,
                longitudeDegrees,
                hoursPerDay),
            _ => CelestialMath.ApplyLongitudeToSolarHours(universal, longitudeDegrees, hoursPerDay)
        };
    }

    private static double PositiveModulo(double value, double modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }
}
