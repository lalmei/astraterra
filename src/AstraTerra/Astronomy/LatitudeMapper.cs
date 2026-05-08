namespace AstraTerra.Astronomy;

public static class LatitudeMapper
{
    public const double WorldLatitudeBandSize = 100000.0;

    public static double MapClimateLatitude(double vintageStoryLatitude)
    {
        return Math.Clamp(vintageStoryLatitude, -1.0, 1.0) * 90.0;
    }

    public static double MapGameLatitude(double z, Func<double, double>? getLatitude)
    {
        return getLatitude is null
            ? MapWorldZ(z)
            : MapClimateLatitude(getLatitude(z));
    }

    public static double MapWorldZ(double z, double originZ = 0)
    {
        return MapRepeatingLatitude((z - originZ) / WorldLatitudeBandSize);
    }

    public static int GetWorldLatitudeWrapCycle(double z, double originZ = 0)
    {
        return (int)Math.Floor((z - originZ) / WorldLatitudeBandSize);
    }

    public static double MapWorldLongitude(double x, double mapSizeX, double mapSizeZ)
    {
        var polarEquatorDistance = mapSizeZ * 0.5;
        if (mapSizeX <= 0 || polarEquatorDistance <= 0)
        {
            return 0;
        }

        var primeMeridianX = mapSizeX * 0.5;
        var circumference = polarEquatorDistance * 4.0;
        var delta = ShortestWrappedDelta(x, primeMeridianX, circumference);
        return NormalizeSignedDegrees(delta / polarEquatorDistance * 90.0);
    }

    public static double MapRepeatingLatitude(double normalizedBandPosition)
    {
        var wrapped = normalizedBandPosition - Math.Floor(normalizedBandPosition);
        var saw = wrapped <= 0.5 ? wrapped * 2.0 : (1.0 - wrapped) * 2.0;
        var sign = wrapped <= 0.5 ? 1.0 : -1.0;
        return sign * saw * 90.0;
    }

    private static double ShortestWrappedDelta(double value, double origin, double period)
    {
        if (period <= 0)
        {
            return value - origin;
        }

        var delta = PositiveModulo(value - origin, period);
        if (delta > period * 0.5)
        {
            delta -= period;
        }

        return delta;
    }

    private static double PositiveModulo(double value, double modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }

    private static double NormalizeSignedDegrees(double degrees)
    {
        var wrapped = degrees % 360.0;
        if (wrapped <= -180.0)
        {
            return wrapped + 360.0;
        }

        return wrapped > 180.0 ? wrapped - 360.0 : wrapped;
    }
}
