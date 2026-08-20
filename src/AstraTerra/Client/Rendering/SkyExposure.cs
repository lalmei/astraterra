using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Whether the player can see the sky from where they are standing.
/// </summary>
/// <remarks>
/// Every sky pass needs this, and none of them can rely on terrain to answer it. The star pass draws
/// during the sun/moon render, before opaque terrain, so in principle terrain painted afterwards
/// occludes it; in practice players report the starfield showing through a cave roof and through
/// house walls, so the mod decides for itself.
/// <para>
/// A rain-map column alone is too blunt: it counts a leaf, a porch or a single overhanging block as
/// a closed sky and takes the whole starfield away from a player standing outdoors under a tree.
/// So the column is only the fast path, and anything below one falls back to how much of the sky's
/// own light reaches the eye — which is high under a canopy or in a doorway, and zero underground.
/// </para>
/// </remarks>
public static class SkyExposure
{
    /// <summary>
    /// How many levels below full daylight the eye may sit and still count as seeing the sky.
    /// </summary>
    /// <remarks>
    /// Sunlight falls by one level per block it spreads, so this is roughly "open sky within eight
    /// blocks": a doorway, a window, a cave mouth a few steps away. Deeper in, and the sky is gone.
    /// </remarks>
    public const int SunlightFalloffAllowance = 8;

    /// <summary>
    /// The pure decision, split from the world lookups so it can be pinned by tests.
    /// </summary>
    /// <param name="rainMapHeight">Height of the highest rain-blocking block in the eye's column.</param>
    /// <param name="eyeHeight">World Y of the player's eye.</param>
    /// <param name="sunlightLevel">Sunlight at the eye, unaffected by the day/night cycle.</param>
    /// <param name="maximumSunlightLevel">Sunlight level of a block under open sky on this world.</param>
    public static bool CanSeeSky(double rainMapHeight, double eyeHeight, int sunlightLevel, int maximumSunlightLevel)
    {
        if (rainMapHeight <= eyeHeight)
        {
            return true;
        }

        return sunlightLevel >= maximumSunlightLevel - SunlightFalloffAllowance;
    }

    public static bool CanSeeSky(ICoreClientAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var entity = api.World.Player?.Entity;
        if (entity is null)
        {
            return false;
        }

        var blockPos = entity.Pos.AsBlockPos;
        var eyeHeight = entity.Pos.InternalY + entity.LocalEyePos.Y;
        var accessor = api.World.BlockAccessor;
        var eyePos = blockPos.Copy();
        eyePos.Y = (int)eyeHeight;

        return CanSeeSky(
            accessor.GetRainMapHeightAt(blockPos),
            eyeHeight,
            accessor.GetLightLevel(eyePos, EnumLightLevelType.OnlySunLight),
            // A world can be configured with its own light curve, so the top of the sunlight table is
            // what "open sky" means here rather than vanilla's 22.
            Math.Max(1, (api.World.SunLightLevels?.Length ?? 23) - 1));
    }
}
