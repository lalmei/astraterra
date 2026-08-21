using AstraTerra.Astronomy;
using OpenTK.Mathematics;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Carries a cached star projection forward to the sky the player is actually looking at.
/// </summary>
/// <remarks>
/// The star buffers are only reprojected once the sky has turned far enough to be worth the work,
/// which leaves every frame in between drawing a sky that is up to that threshold stale. At the
/// naked eye the lag is under a pixel. Through a telescope the same fraction of a degree covers
/// several, and the sky visibly steps rather than turns.
/// <para>
/// It does not need reprojecting, though: between two sidereal times the whole celestial sphere has
/// simply turned about the celestial pole, rigidly, taking every star with it. That is one rotation
/// applied to the batched mesh, so the sky moves every frame at no per-star cost, and the threshold
/// goes back to deciding only how often brightness and horizon culling are re-evaluated — which is
/// what it was always safe for.
/// </para>
/// </remarks>
public static class SkyResidualRotation
{
    /// <summary>
    /// The rotation carrying a projection made at one sidereal angle to another.
    /// </summary>
    /// <param name="latitudeDeg">
    /// The latitude the projection was made at, which is where the pole sits on its horizon. A
    /// player who has walked far enough north to move the pole has also crossed the projection's own
    /// latitude threshold, so the cached buffers are rebuilt before that error can accumulate.
    /// </param>
    /// <param name="siderealDeltaDeg">
    /// How much further the sky has turned since, signed and already shortest-way normalized.
    /// </param>
    public static Matrix4 Build(double latitudeDeg, double siderealDeltaDeg)
    {
        // Not-a-number is what a dropped projection leaves behind, and there is nothing cached for a
        // rotation to carry forward in that case.
        if (siderealDeltaDeg == 0.0 || !double.IsFinite(siderealDeltaDeg) || !double.IsFinite(latitudeDeg))
        {
            return Matrix4.Identity;
        }

        // The pole is due north at the observer's latitude, which is the axis the sky turns on. The
        // angle is negated because rising sidereal time is an increasing hour angle, and a star's
        // hour angle grows as it moves west.
        var (poleX, poleY, poleZ) = SkyProjection.GetWorldDirection(0.0, latitudeDeg);
        var axis = new Vector3((float)poleX, (float)poleY, (float)poleZ);

        return Matrix4.CreateFromAxisAngle(axis, (float)(-siderealDeltaDeg * Math.PI / 180.0));
    }
}
