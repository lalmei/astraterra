namespace AstraTerra.Astronomy;

/// <summary>
/// Ties the world clock to the real calendar that orbital elements are expressed in.
/// </summary>
/// <remarks>
/// Orbits are published against Julian centuries past J2000, so a world day has to be worth
/// something in real time before a planet can be placed at all. Two decisions are baked in here, and
/// both are visible in the sky.
/// </remarks>
public static class WorldEpoch
{
    /// <summary>
    /// Real days a world year is worth. One world year is one Julian year, whatever the world's
    /// <c>daysPerYear</c>, so Jupiter takes about twelve <em>world</em> years on a 12-day world and a
    /// 360-day world alike.
    /// </summary>
    /// <remarks>
    /// The alternative — scaling to real time — leaves planets effectively motionless on a short
    /// world and puts Jupiter's return roughly 4300 world days away. Scaling to world years keeps
    /// the orbital relationships a player can actually watch: an inferior planet swings either side
    /// of the sun within a season, and a superior one comes to opposition about once a world year.
    /// </remarks>
    public const double RealDaysPerWorldYear = 365.25;

    /// <summary>Days in the Julian century the published element rates are quoted against.</summary>
    public const double DaysPerJulianCentury = 36525.0;

    /// <summary>Real days from J2000.0 to the March equinox of 2000.</summary>
    public const double EquinoxOffsetDaysFromJ2000 = 78.816;

    /// <summary>
    /// Real days from J2000.0 to the date a world clock of zero is taken to mean, which is the start
    /// of the world year.
    /// </summary>
    /// <remarks>
    /// The world's own seasons fix this: <see cref="CelestialMath.GetSolarLongitudeDegrees"/> reads
    /// zero at the March equinox, and that equinox falls
    /// <see cref="CelestialMath.SpringEquinoxYearFraction"/> of the way into the world year rather
    /// than at its start. Anchoring the planets anywhere else would start them that far round Earth's
    /// orbit from where the world says its own sun is, putting every planet in the wrong season — a
    /// body at opposition would be drawn near the sun, and nothing would fail.
    /// <para>
    /// It lands a couple of days before J2000 itself, because a world year begins where vanilla's
    /// year does: at the first of January.
    /// </para>
    /// </remarks>
    public const double WorldZeroOffsetDaysFromJ2000 =
        EquinoxOffsetDaysFromJ2000 - (CelestialMath.SpringEquinoxYearFraction * RealDaysPerWorldYear);

    /// <summary>
    /// Julian centuries past J2000 for a world time, which is the argument every published element
    /// rate is quoted against.
    /// </summary>
    public static double GetJulianCenturies(double totalDays, int daysPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        var worldYears = totalDays / daysPerYear;
        return (WorldZeroOffsetDaysFromJ2000 + (worldYears * RealDaysPerWorldYear)) / DaysPerJulianCentury;
    }
}
