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

    /// <summary>
    /// How far round its orbit the world has travelled since the equinox, in degrees. This is the
    /// seasonal term of <see cref="GetVanillaAlignedLocalSiderealAngle"/>, which is the sun's right
    /// ascension at local noon and therefore stands in for solar longitude.
    /// </summary>
    /// <remarks>
    /// Season-anchored events belong on this angle rather than a day of the year: <c>daysPerYear</c>
    /// is world configuration, so day 224 lands in a different season on every world while 140 deg
    /// of solar longitude is late summer on all of them. Extracted from the sidereal angle rather
    /// than recomputed so the two can never drift apart.
    /// </remarks>
    public static double GetSolarLongitudeDegrees(double totalDays, int daysPerYear, double equinoxDayOfYear = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        var dayOfYear = PositiveModulo(totalDays, daysPerYear);
        var seasonalTurns = PositiveModulo(dayOfYear - equinoxDayOfYear, daysPerYear) / daysPerYear;
        return seasonalTurns * 360.0;
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

        var solarLongitudeDeg = GetSolarLongitudeDegrees(totalDays, daysPerYear, equinoxDayOfYear);
        var localSolarHours = PositiveModulo(totalDays * hoursPerDay, hoursPerDay) + longitudeDegrees / 15.0;

        // The sun transits at local noon, so sidereal time equals the sun's right ascension
        // (the seasonal term) at that moment and gains 15 deg for every solar hour after it.
        // The hour angle in GetHorizontalCoordinates is sidereal - right ascension, so sidereal
        // must increase with time for the sky to turn east to west.
        return NormalizeDegrees(solarLongitudeDeg + ((localSolarHours - 12.0) * 15.0));
    }

    /// <summary>
    /// Hour of the world day for a calendar timestamp, matching the clock the game itself shows.
    /// Longitude is deliberately left out: vanilla's clock does not shift with world X either.
    /// </summary>
    public static double GetLocalSolarTimeHours(double totalDays, double hoursPerDay)
    {
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        return PositiveModulo(totalDays * hoursPerDay, hoursPerDay);
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

    /// <summary>
    /// Signed shortest way round from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/>,
    /// in the range (-180, 180]. Positive means <paramref name="toDegrees"/> is ahead.
    /// </summary>
    /// <remarks>
    /// Anything that measures "how far from" an angle needs this rather than a plain subtraction, or
    /// a window straddling 0/360 reads as a full turn wide instead of a few degrees.
    /// </remarks>
    public static double ShortestAngularDistanceDegrees(double fromDegrees, double toDegrees)
    {
        var difference = NormalizeDegrees(toDegrees - fromDegrees);
        return difference > 180.0 ? difference - 360.0 : difference;
    }

    private static double PositiveModulo(double value, double modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
