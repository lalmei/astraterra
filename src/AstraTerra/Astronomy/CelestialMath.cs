namespace AstraTerra.Astronomy;

public readonly record struct HorizontalCoordinates(double AzimuthDeg, double AltitudeDeg);

/// <summary>
/// A position on the celestial sphere, in the frame every sky object shares.
/// </summary>
/// <remarks>
/// A catalog object carries one of these as a constant; a body that moves resolves one from an
/// <see cref="ISkyEphemeris"/> at a world time. Everything downstream of that — projection, the
/// sextant, the astrolabe — sees only this shape, which is what lets one path serve stars, deep-sky
/// objects, planets and comets alike.
/// </remarks>
public readonly record struct EquatorialCoordinates(double RightAscensionDeg, double DeclinationDeg);

public static class CelestialMath
{
    /// <summary>
    /// Tilt of the ecliptic — the plane the planets orbit in — against the celestial equator, in
    /// degrees. Earth's mean obliquity at J2000.
    /// </summary>
    /// <remarks>
    /// Anything that starts life in ecliptic coordinates needs this to reach the equatorial frame
    /// the rest of the sky lives in: planet and comet positions arrive as ecliptic longitude and
    /// latitude, and the sun's declination is this same angle projected onto the year. Landed once
    /// here so those cannot disagree about the tilt of the world they share.
    /// <para>
    /// The 128-year drift in the real value is far below anything a billboard in a voxel sky can
    /// show, so it is treated as a constant.
    /// </para>
    /// </remarks>
    public const double MeanObliquityDeg = 23.4392911;

    /// <summary>
    /// Where the March equinox falls in the world year, as a fraction of it.
    /// </summary>
    /// <remarks>
    /// Vintage Story's own sun runs its declination as <c>-tilt * cos(2*pi*(yearRel + 10/365))</c>,
    /// so it crosses the equator northward a quarter turn before that cosine peaks. Day zero of a
    /// world year is therefore the first of January, not the equinox, and a sky anchored at day zero
    /// runs about 80 deg of solar longitude ahead of the world it is drawn over: Betelgeuse would
    /// come to the meridian at midnight in late September instead of late December.
    /// <para>
    /// This is the one number that ties the star sky to the season the game itself is showing, which
    /// is why it is taken from vanilla's sun rather than from the calendar's season thresholds --
    /// those sit two days earlier and it is the sun a player can actually watch.
    /// </para>
    /// </remarks>
    public const double SpringEquinoxYearFraction = 0.25 - (10.0 / 365.0);

    /// <summary>
    /// The day of the world year the March equinox falls on, for a world of
    /// <paramref name="daysPerYear"/> days.
    /// </summary>
    public static double GetEquinoxDayOfYear(int daysPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        return daysPerYear * SpringEquinoxYearFraction;
    }

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
    public static double GetSolarLongitudeDegrees(double totalDays, int daysPerYear, double? equinoxDayOfYear = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        var equinoxDay = equinoxDayOfYear ?? GetEquinoxDayOfYear(daysPerYear);
        var dayOfYear = PositiveModulo(totalDays, daysPerYear);
        var seasonalTurns = PositiveModulo(dayOfYear - equinoxDay, daysPerYear) / daysPerYear;
        return seasonalTurns * 360.0;
    }

    /// <summary>
    /// The visible survival sun in the equatorial frame used by the star catalogue.
    /// </summary>
    /// <remarks>
    /// Vintage Story advances the seasons uniformly and applies axial tilt as a sinusoidal solar
    /// declination. Right ascension is not ecliptic longitude: the shared obliquity rotation supplies
    /// it, while the declination expression deliberately mirrors the survival sun that instruments
    /// and the player actually see.
    /// </remarks>
    public static EquatorialCoordinates GetVanillaAlignedSolarEquatorialCoordinates(
        double totalDays,
        int daysPerYear,
        double? equinoxDayOfYear = null)
    {
        var solarLongitudeDeg = GetSolarLongitudeDegrees(totalDays, daysPerYear, equinoxDayOfYear);
        var rotated = EclipticToEquatorial(solarLongitudeDeg, eclipticLatitudeDeg: 0.0);
        var declinationDeg = MeanObliquityDeg * Math.Sin(ToRadians(solarLongitudeDeg));

        return new EquatorialCoordinates(rotated.RightAscensionDeg, declinationDeg);
    }

    public static double GetVanillaAlignedLocalSiderealAngle(
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        double longitudeDegrees = 0,
        double? equinoxDayOfYear = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        var solar = GetVanillaAlignedSolarEquatorialCoordinates(
            totalDays,
            daysPerYear,
            equinoxDayOfYear);
        var universalDayFraction = GetUniversalSolarTimeHours(totalDays, hoursPerDay) / hoursPerDay;
        var localDayFraction = PositiveModulo(universalDayFraction + longitudeDegrees / 360.0, 1.0);
        var solarHourAngleDeg = (localDayFraction - 0.5) * 360.0;

        // The visible sun transits at local noon, when its hour angle is zero. Sidereal time is
        // solar right ascension plus hour angle; deriving the daily term from a fraction of one
        // rotation keeps custom-length world days on the same sky as the solar delegate.
        return NormalizeDegrees(solar.RightAscensionDeg + solarHourAngleDeg);
    }

    /// <summary>Hour on the world's single universal clock, with no observer-position offset.</summary>
    public static double GetUniversalSolarTimeHours(double totalDays, double hoursPerDay)
    {
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        return PositiveModulo(totalDays * hoursPerDay, hoursPerDay);
    }

    /// <summary>
    /// Local apparent solar time at an observer longitude. One full trip around the world shifts by
    /// one complete world day, including on worlds that do not divide a day into 24 hours.
    /// </summary>
    public static double GetLocalSolarTimeHours(
        double totalDays,
        double hoursPerDay,
        double longitudeDegrees = 0)
        => ApplyLongitudeToSolarHours(
            GetUniversalSolarTimeHours(totalDays, hoursPerDay),
            longitudeDegrees,
            hoursPerDay);

    public static double ApplyLongitudeToSolarHours(
        double universalSolarHours,
        double longitudeDegrees,
        double hoursPerDay)
    {
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        return PositiveModulo(
            universalSolarHours + longitudeDegrees / 360.0 * hoursPerDay,
            hoursPerDay);
    }

    /// <summary>
    /// Discrete whole-clock-hour zones. A 24-hour world has 24 zones; a 16-hour world has 16.
    /// </summary>
    public static double ApplyTimeZoneToSolarHours(
        double universalSolarHours,
        double longitudeDegrees,
        double hoursPerDay)
    {
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        var zoneOffsetHours = Math.Round(longitudeDegrees / 360.0 * hoursPerDay);
        return PositiveModulo(universalSolarHours + zoneOffsetHours, hoursPerDay);
    }

    /// <summary>
    /// Shifts the fractional day passed to vanilla's sun delegate by observer longitude.
    /// </summary>
    public static float ApplyLongitudeToDayRel(float dayRel, double longitudeDegrees)
        => (float)PositiveModulo(dayRel + longitudeDegrees / 360.0, 1.0);

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

    /// <summary>
    /// Reads a sighting back into the fixed sky: where a body at this altitude and bearing, seen
    /// from this latitude at this sidereal angle, sits among the stars.
    /// </summary>
    /// <remarks>
    /// The exact inverse of <see cref="GetHorizontalCoordinates"/>, and the arithmetic that makes a
    /// ledger of sightings worth keeping. Two entries taken nights apart are only comparable once
    /// the sky's turning has been divided out, because a fixed star sits at a different altitude and
    /// bearing every hour while never leaving its place.
    /// </remarks>
    public static EquatorialCoordinates GetEquatorialCoordinates(
        double azimuthDeg,
        double altitudeDeg,
        double latitudeDeg,
        double localSiderealDeg)
    {
        var azimuth = ToRadians(NormalizeDegrees(azimuthDeg));
        var altitude = ToRadians(altitudeDeg);
        var latitude = ToRadians(latitudeDeg);

        var sinDeclination = (Math.Sin(altitude) * Math.Sin(latitude))
                             + (Math.Cos(altitude) * Math.Cos(latitude) * Math.Cos(azimuth));
        var declination = Math.Asin(Math.Clamp(sinDeclination, -1.0, 1.0));

        var hourAngleY = -Math.Sin(azimuth) * Math.Cos(altitude);
        var hourAngleX = (Math.Sin(altitude) * Math.Cos(latitude))
                         - (Math.Cos(altitude) * Math.Sin(latitude) * Math.Cos(azimuth));
        var hourAngleDeg = ToDegrees(Math.Atan2(hourAngleY, hourAngleX));

        return new EquatorialCoordinates(
            NormalizeDegrees(localSiderealDeg - hourAngleDeg),
            ToDegrees(declination));
    }

    /// <summary>
    /// Rotates a position from the ecliptic frame, where the planets are computed, into the
    /// equatorial frame the sky is drawn in.
    /// </summary>
    /// <param name="obliquityDeg">
    /// Defaults to <see cref="MeanObliquityDeg"/>. Exposed so a world with a different tilt — or a
    /// test pinning the rotation itself — can supply its own.
    /// </param>
    /// <remarks>
    /// Done through a unit vector rather than the textbook <c>atan2(sin l cos e - tan b sin e,
    /// cos l)</c>, which is the same rotation but divides by zero at the ecliptic poles. Planets
    /// never go near them; a comet on a steep orbit can.
    /// </remarks>
    public static EquatorialCoordinates EclipticToEquatorial(
        double eclipticLongitudeDeg,
        double eclipticLatitudeDeg,
        double obliquityDeg = MeanObliquityDeg)
    {
        var longitude = ToRadians(eclipticLongitudeDeg);
        var latitude = ToRadians(eclipticLatitudeDeg);
        var obliquity = ToRadians(obliquityDeg);
        var cosLatitude = Math.Cos(latitude);

        var x = cosLatitude * Math.Cos(longitude);
        var y = (cosLatitude * Math.Sin(longitude) * Math.Cos(obliquity)) - (Math.Sin(latitude) * Math.Sin(obliquity));
        var z = (cosLatitude * Math.Sin(longitude) * Math.Sin(obliquity)) + (Math.Sin(latitude) * Math.Cos(obliquity));

        return new EquatorialCoordinates(
            NormalizeDegrees(ToDegrees(Math.Atan2(y, x))),
            ToDegrees(Math.Asin(Math.Clamp(z, -1.0, 1.0))));
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
