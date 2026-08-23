namespace AstraTerra.Astronomy;

/// <summary>A direction in the galactic frame: longitude along the plane, latitude away from it.</summary>
public readonly record struct GalacticCoordinates(double LongitudeDeg, double LatitudeDeg);

/// <summary>
/// Converts galactic directions into the equatorial frame the rest of the sky is drawn in.
/// </summary>
/// <remarks>
/// The Milky Way is the one thing in the mod that is naturally described in galactic coordinates:
/// its glow is a disc seen edge-on from inside, so it is a band at latitude zero and nothing else.
/// Every other body arrives already in right ascension and declination, so this rotation exists in
/// exactly one place and is applied once per band vertex.
/// <para>
/// The three constants are the J2000 definition of the frame: where the north galactic pole sits in
/// equatorial coordinates, and which galactic longitude the north celestial pole falls on.
/// </para>
/// </remarks>
public static class GalacticFrame
{
    /// <summary>Right ascension of the north galactic pole, J2000.</summary>
    public const double NorthPoleRightAscensionDeg = 192.85948;

    /// <summary>Declination of the north galactic pole, J2000.</summary>
    public const double NorthPoleDeclinationDeg = 27.12825;

    /// <summary>Galactic longitude of the north celestial pole, J2000.</summary>
    public const double NorthCelestialPoleLongitudeDeg = 122.93192;

    /// <summary>
    /// The equatorial position a galactic direction points at.
    /// </summary>
    /// <remarks>
    /// Written through sines and cosines of the pole rather than a matrix because it is only ever
    /// this one rotation, and because a spelt-out formula can be checked against the three constants
    /// above by eye: at galactic latitude +90 it has to return the pole itself, and it does.
    /// </remarks>
    public static EquatorialCoordinates ToEquatorial(double longitudeDeg, double latitudeDeg)
    {
        var poleDeclination = ToRadians(NorthPoleDeclinationDeg);
        var latitude = ToRadians(latitudeDeg);
        var longitudeFromPole = ToRadians(NorthCelestialPoleLongitudeDeg - longitudeDeg);

        var sinLatitude = Math.Sin(latitude);
        var cosLatitude = Math.Cos(latitude);
        var sinDeclination =
            (Math.Sin(poleDeclination) * sinLatitude) +
            (Math.Cos(poleDeclination) * cosLatitude * Math.Cos(longitudeFromPole));
        var declination = ToDegrees(Math.Asin(Math.Clamp(sinDeclination, -1.0, 1.0)));

        var y = cosLatitude * Math.Sin(longitudeFromPole);
        var x =
            (Math.Cos(poleDeclination) * sinLatitude) -
            (Math.Sin(poleDeclination) * cosLatitude * Math.Cos(longitudeFromPole));
        var rightAscension = CelestialMath.NormalizeDegrees(
            NorthPoleRightAscensionDeg + ToDegrees(Math.Atan2(y, x)));

        return new EquatorialCoordinates(rightAscension, declination);
    }

    /// <summary>The galactic direction of an equatorial position, for tests and debug reporting.</summary>
    public static GalacticCoordinates FromEquatorial(double rightAscensionDeg, double declinationDeg)
    {
        var poleDeclination = ToRadians(NorthPoleDeclinationDeg);
        var declination = ToRadians(declinationDeg);
        var rightAscensionFromPole = ToRadians(rightAscensionDeg - NorthPoleRightAscensionDeg);

        var sinDeclination = Math.Sin(declination);
        var cosDeclination = Math.Cos(declination);
        var sinLatitude =
            (Math.Sin(poleDeclination) * sinDeclination) +
            (Math.Cos(poleDeclination) * cosDeclination * Math.Cos(rightAscensionFromPole));
        var latitude = ToDegrees(Math.Asin(Math.Clamp(sinLatitude, -1.0, 1.0)));

        var y = cosDeclination * Math.Sin(rightAscensionFromPole);
        var x =
            (Math.Cos(poleDeclination) * sinDeclination) -
            (Math.Sin(poleDeclination) * cosDeclination * Math.Cos(rightAscensionFromPole));
        var longitude = CelestialMath.NormalizeDegrees(
            NorthCelestialPoleLongitudeDeg - ToDegrees(Math.Atan2(y, x)));

        return new GalacticCoordinates(longitude, latitude);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
