namespace AstraTerra.Astronomy;

public readonly record struct HorizontalCoordinates(double AzimuthDeg, double AltitudeDeg);

public static class CelestialMath
{
    public static double GetSeasonalAngle(int dayOfYear, double dayFraction, int daysPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        return ((dayOfYear + dayFraction) / daysPerYear) * 360.0;
    }

    public static double GetLocalSiderealAngle(int dayOfYear, double dayFraction, int daysPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        return NormalizeDegrees((dayFraction * 360.0) + GetSeasonalAngle(dayOfYear, dayFraction, daysPerYear));
    }

    public static double GetVanillaAlignedLocalSiderealAngle(
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        double longitudeDegrees = 0,
        double equinoxDayOfYear = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        var dayOfYear = PositiveModulo(totalDays, daysPerYear);
        var seasonalTurns = PositiveModulo(dayOfYear - equinoxDayOfYear, daysPerYear) / daysPerYear;
        var localSolarHours = PositiveModulo(totalDays * hoursPerDay, hoursPerDay) + longitudeDegrees / 15.0;
        return NormalizeDegrees((seasonalTurns * 360.0) + ((12.0 - localSolarHours) * 15.0));
    }

    public static double ClassifyAltitudeDeg(double rightAscensionDeg, double declinationDeg, double latitudeDeg, double localSiderealDeg)
        => GetHorizontalCoordinates(rightAscensionDeg, declinationDeg, latitudeDeg, localSiderealDeg).AltitudeDeg;

    public static HorizontalCoordinates GetHorizontalCoordinates(
        double rightAscensionDeg,
        double declinationDeg,
        double latitudeDeg,
        double localSiderealDeg)
    {
        var hourAngle = ToRadians(NormalizeDegrees(localSiderealDeg - rightAscensionDeg));
        var declination = ToRadians(declinationDeg);
        var latitude = ToRadians(latitudeDeg);
        var sinAltitude = Math.Sin(declination) * Math.Sin(latitude) +
                          Math.Cos(declination) * Math.Cos(latitude) * Math.Cos(hourAngle);
        var altitude = ToDegrees(Math.Asin(Math.Clamp(sinAltitude, -1.0, 1.0)));
        var azimuthY = -Math.Sin(hourAngle);
        var azimuthX = Math.Tan(declination) * Math.Cos(latitude) - Math.Sin(latitude) * Math.Cos(hourAngle);
        var azimuth = NormalizeDegrees(ToDegrees(Math.Atan2(azimuthY, azimuthX)));
        return new HorizontalCoordinates(azimuth, altitude);
    }

    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static double PositiveModulo(double value, double modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
