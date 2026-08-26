using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>
/// How many of the observer's own constellations a disc will carry.
/// </summary>
/// <remarks>
/// The rim stops improving at copper, so past that a disc is bought for what it can be engraved
/// with rather than for what it can measure. Bronze takes two figures — the count the real artefact
/// carries, and a budget tight enough that choosing between an elaborate figure and two compact ones
/// is a real choice. Copper takes one. Clay takes none: a figure is laid into metal, and a disc that
/// cannot hold one is a disc you outgrow rather than keep, which is exactly what the starter disc
/// should be.
/// </remarks>
public static class SkyDiscEngraving
{
    /// <summary>What bronze carries, and the most any disc will.</summary>
    public const int MaxFigures = 2;

    /// <summary>How many figures this disc has room for. None, unless its material says otherwise.</summary>
    public static int FiguresAllowed(ItemStack? stack)
        => Math.Clamp(
            stack?.Collectible?.Attributes?["engravedFigures"].AsInt(0) ?? 0,
            0,
            MaxFigures);
}
