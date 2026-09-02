using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

/// <summary>
/// The observer longitude the rest of the sky is allowed to use.
/// </summary>
/// <remarks>
/// Longitude only means something to the star field, the instruments and the displayed clock while
/// the sun it is measured against also moves with it. When the longitude-aware sun is switched off
/// on the server, or when another mod has taken the solar delegate back, the world keeps a single
/// solar time everywhere -- and so must everything AstraTerra draws over it, or the stars would
/// transit at an hour the visible sun disagrees with.
/// <para>
/// Client-scope state, registered by the client's installer. With nothing registered this reads as
/// zero, which is also the honest answer before the server has said what its policy is.
/// </para>
/// </remarks>
public static class ObserverLongitude
{
    private static Func<bool>? sunFollowsLongitude;

    /// <summary>Whether the sun the player can see is currently shifted by longitude.</summary>
    public static bool SunFollowsLongitude => sunFollowsLongitude?.Invoke() ?? false;

    public static void FollowSun(Func<bool> sunIsLongitudeAware)
    {
        ArgumentNullException.ThrowIfNull(sunIsLongitudeAware);
        sunFollowsLongitude = sunIsLongitudeAware;
    }

    public static void Reset() => sunFollowsLongitude = null;

    /// <summary>Observer longitude in degrees, or zero while the sun ignores longitude.</summary>
    public static double ForObserver(double x, IWorldAccessor world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return SunFollowsLongitude ? LatitudeMapper.MapWorldLongitude(x, world) : 0.0;
    }
}
