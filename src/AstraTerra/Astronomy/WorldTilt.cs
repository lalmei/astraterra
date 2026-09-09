namespace AstraTerra.Astronomy;

/// <summary>
/// How far this world's axis leans out of its orbit, and the sun that follows from it.
/// </summary>
/// <remarks>
/// <para>
/// Vintage Story has one tilt, Earth's, written into the survival mod as a field and used in one
/// place: the delegate that says where the sun is. Everything a tilt decides comes off that one
/// number -- how far north the sun gets in summer, whether a latitude has a polar night, how long
/// the days are in between -- so substituting it is enough to give a world a different axis, and
/// substituting it in the delegate rather than anywhere else is what keeps the sun a player sees,
/// the sun the light is computed from, and the sun an instrument measures the same sun.
/// </para>
/// <para>
/// A generated world is where this comes from. A tidally locked moon does not choose its own tilt:
/// it is locked to its giant and orbits in that giant's equatorial plane, so its axis is the
/// giant's axis, and the giant's obliquity is the moon's. Nothing here generates that -- it is
/// handed in through <c>AstraTerraModSystem.SetWorldObliquity</c> -- and with nothing handed in
/// this whole file is Earth's tilt and vanilla's sun, unchanged.
/// </para>
/// </remarks>
public static class WorldTilt
{
    /// <summary>
    /// The tilt Vintage Story's own sun runs on, in degrees.
    /// </summary>
    /// <remarks>
    /// Read off <c>SurvivalCoreSystem.EarthAxialTilt</c>, which is a <c>float</c> holding
    /// <c>0.40910518</c> radians. That is Earth's mean obliquity to a hair -- it differs from
    /// <see cref="CelestialMath.MeanObliquityDeg"/> in the sixth decimal place, which is a float's
    /// rounding rather than a disagreement about the world. Kept separately anyway, because this is
    /// the number that has to be reproduced exactly when the substitution is off.
    /// </remarks>
    public const double VanillaAxialTiltRad = 0.40910518;

    /// <summary>
    /// Where the March equinox falls in vanilla's year, as a fraction of it. The <c>10/365</c> in
    /// the survival sun's own cosine.
    /// </summary>
    public const double SolarPhaseYearFraction = 10.0 / 365.0;

    /// <summary>
    /// The furthest a world may be tipped and still be handled here, in degrees.
    /// </summary>
    /// <remarks>
    /// Ninety degrees is a world lying in its own orbit -- one pole at the star at midsummer, the
    /// other at midwinter -- and it is the end of the scale rather than a limit chosen for taste: a
    /// tilt past ninety is the same geometry with the world turning the other way, which is a
    /// question about its rotation and not about its axis. A generator that wants Uranus should
    /// hand over what it means, and the clamp is here so an arithmetic slip cannot invert a sky.
    /// </remarks>
    public const double MaxTiltDeg = 90.0;

    private static double currentDeg = CelestialMath.MeanObliquityDeg;

    /// <summary>
    /// The tilt every part of this sky is currently working to, in degrees. Earth's until a
    /// generator says otherwise.
    /// </summary>
    /// <remarks>
    /// Ambient rather than passed, because the consumers are spread from the solar delegate to a
    /// bronze disc in a player's hand, and threading a parameter through all of them would put the
    /// world's tilt into the signature of every instrument. Both sides hold the same value for the
    /// same reason they hold the same near-body light source -- it comes from one generator, which
    /// publishes it to both -- so unlike observer longitude this is not client-scope state that a
    /// server would read wrongly.
    /// </remarks>
    public static double CurrentDeg => currentDeg;

    /// <summary>Whether this world is running on anything other than Earth's tilt.</summary>
    public static bool IsGenerated => currentDeg != CelestialMath.MeanObliquityDeg;

    /// <summary>
    /// Sets the world's tilt, or returns it to Earth's by passing null. Values outside
    /// <c>[0, <see cref="MaxTiltDeg"/>]</c> are clamped, and a value that is not a number is
    /// ignored.
    /// </summary>
    public static void Set(double? obliquityDeg)
    {
        currentDeg = obliquityDeg is { } deg && double.IsFinite(deg)
            ? Math.Clamp(deg, 0.0, MaxTiltDeg)
            : CelestialMath.MeanObliquityDeg;
    }

    /// <summary>Returns the world to Earth's tilt.</summary>
    public static void Reset() => Set(null);

    /// <summary>
    /// The sun's declination at a point in the year, for a world of this tilt, in radians.
    /// </summary>
    /// <remarks>
    /// Vanilla's own expression, <c>-tilt * cos(2*pi*(yearRel + 10/365))</c>, with the tilt left
    /// open. The phase is not: it is where the game puts the solstices, and moving it would move
    /// the seasons out from under the calendar that names them.
    /// </remarks>
    public static double SolarDeclinationRad(double yearRel, double tiltRad)
        => -tiltRad * Math.Cos(2.0 * Math.PI * (yearRel + SolarPhaseYearFraction));

    /// <summary>
    /// Where the sun stands, in the spherical coordinates Vintage Story's calendar asks its solar
    /// delegate for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the survival mod's own arithmetic, restated with the tilt as a parameter: the sun's
    /// altitude from the spherical law of cosines on latitude, declination and hour angle, then the
    /// azimuth from the same triangle, then both turned into the zenith-and-azimuth convention the
    /// calendar wants. Nothing here is an improvement on it. Reproducing it exactly is the whole
    /// point -- a world at Earth's tilt has to come out of this with the sun it already had, or
    /// every sky in the game shifts the day this mod is installed.
    /// </para>
    /// <para>
    /// <paramref name="latitudeRad"/> is the game's own latitude, which runs to a right angle at
    /// the poles rather than to ninety of anything, so a caller converts with
    /// <c>OnGetLatitude(posZ) * pi/2</c> the way the survival delegate does.
    /// </para>
    /// </remarks>
    public static (double ZenithAngle, double AzimuthAngle) SolarSphericalCoords(
        double latitudeRad,
        double yearRel,
        double dayRel,
        double tiltRad)
    {
        var hourAngle = 2.0 * Math.PI * (dayRel - 0.5);
        var declination = SolarDeclinationRad(yearRel, tiltRad);
        var sinLatitude = Math.Sin(latitudeRad);
        var cosLatitude = Math.Cos(latitudeRad);
        var sinDeclination = Math.Sin(declination);

        var sinAltitude = Math.Clamp(
            (sinLatitude * sinDeclination)
                + (cosLatitude * Math.Cos(declination) * Math.Cos(hourAngle)),
            -1.0,
            1.0);
        var cosAltitude = Math.Sqrt(1.0 - (sinAltitude * sinAltitude));

        // The epsilon is vanilla's, and it is what keeps the azimuth finite at the poles, where the
        // triangle this comes from has no width.
        var azimuth = Math.Acos(
            Math.Clamp(
                ((sinLatitude * sinAltitude) - sinDeclination)
                    / ((cosLatitude * cosAltitude) + 1.0000000116860974E-07),
                -1.0,
                1.0));
        azimuth = PositiveModulo(
            Math.PI + (Math.Sign(hourAngle) * azimuth),
            2.0 * Math.PI);

        return (
            (2.0 * Math.PI) - Math.Acos(sinAltitude),
            (2.0 * Math.PI) - azimuth);
    }

    public static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

    private static double PositiveModulo(double value, double modulus)
    {
        var remainder = value % modulus;
        return remainder < 0.0 ? remainder + modulus : remainder;
    }
}
