using AstraTerra.Astronomy;

namespace AstraTerra.Config;

public static class ClockDisplay
{
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
}
