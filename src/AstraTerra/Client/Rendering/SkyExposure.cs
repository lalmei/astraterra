using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Whether the sky can be reached from where the player is standing.
/// </summary>
/// <remarks>
/// Only the passes that draw <em>after</em> terrain need this. The star pass draws inside Vintage
/// Story's sun/moon render, before opaque terrain, so terrain painted afterwards occludes it per
/// pixel — the same slot and the same mechanism vanilla's own `SystemRenderNightSky` relies on, which
/// carries no exposure check of any kind and is still not visible from inside a cave. The sky grid
/// (Opaque 0.96), the constellation overlay and the sextant readout (both Ortho) draw over finished
/// terrain and cannot be occluded, so they ask here instead.
/// <para>
/// The test is deliberately weak: <em>any</em> sunlight at the eye counts. Sunlight and line of sight
/// travel through the same openings, so a doorway, a window, a canopy, a porch or a cave mouth all
/// pass, while a sealed room or the depths of a cave — where no sky is visible in any direction —
/// do not. An earlier version demanded sunlight within eight levels of full daylight, which took the
/// whole sky away from a player standing indoors with the sky in plain view through a window.
/// </para>
/// </remarks>
public static class SkyExposure
{
    /// <summary>
    /// The pure decision, split from the world lookups so it can be pinned by tests.
    /// </summary>
    /// <param name="rainMapHeight">Height of the highest rain-blocking block in the eye's column.</param>
    /// <param name="eyeHeight">World Y of the player's eye.</param>
    /// <param name="sunlightLevel">Sunlight at the eye, unaffected by the day/night cycle.</param>
    public static bool CanSeeSky(double rainMapHeight, double eyeHeight, int sunlightLevel)
        => rainMapHeight <= eyeHeight || sunlightLevel > 0;

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
        var rainMapHeight = accessor.GetRainMapHeightAt(blockPos);

        // Reuses the one position rather than copying it: this runs on the render thread, once per
        // gated pass per frame.
        blockPos.Y = (int)eyeHeight;

        return CanSeeSky(rainMapHeight, eyeHeight, accessor.GetLightLevel(blockPos, EnumLightLevelType.OnlySunLight));
    }

    /// <summary>
    /// The numbers behind the verdict, for <c>.stars debug</c>. A player reporting "I can/cannot see
    /// stars here" can then report why, rather than leaving it to be guessed at from a screenshot.
    /// </summary>
    public static string Describe(ICoreClientAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var entity = api.World.Player?.Entity;
        if (entity is null)
        {
            return "skyExposure=unknown";
        }

        var blockPos = entity.Pos.AsBlockPos;
        var eyeHeight = entity.Pos.InternalY + entity.LocalEyePos.Y;
        var accessor = api.World.BlockAccessor;
        var rainMapHeight = accessor.GetRainMapHeightAt(blockPos);
        blockPos.Y = (int)eyeHeight;
        var sunlight = accessor.GetLightLevel(blockPos, EnumLightLevelType.OnlySunLight);

        return $"skyExposure={(CanSeeSky(rainMapHeight, eyeHeight, sunlight) ? "open" : "blocked")}; " +
               $"eyeY={eyeHeight:0.0}; rainMapY={rainMapHeight}; sunlightAtEye={sunlight}; " +
               $"note=the star pass is occluded by terrain and is not gated on this";
    }
}
