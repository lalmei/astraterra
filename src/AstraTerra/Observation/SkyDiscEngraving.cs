using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>
/// How many of the observer's own constellations a disc will carry.
/// </summary>
/// <remarks>
/// The rim stops improving at copper, so past that a disc is bought for what it can be engraved
/// with rather than for what it can measure. Metal takes one figure: a disc is one object with one
/// face, and the figure on it is the one its owner thought worth carrying rather than a notebook
/// page of them. Clay takes none — a figure is laid into metal, and a disc that cannot hold one is
/// a disc you outgrow rather than keep, which is exactly what the starter disc should be.
/// </remarks>
public static class SkyDiscEngraving
{
    /// <summary>What metal carries, and the most any disc will.</summary>
    public const int MaxFigures = 1;

    /// <summary>
    /// The id the disc's figure answers to. A disc holds one figure and never numbers it, but the
    /// drawing that puts constellations in the sky wants an id to tell one from another.
    /// </summary>
    public const int FigureId = -1;

    /// <summary>How many figures this disc has room for. None, unless its material says otherwise.</summary>
    public static int FiguresAllowed(ItemStack? stack)
        => Math.Clamp(
            stack?.Collectible?.Attributes?["engravedFigures"].AsInt(0) ?? 0,
            0,
            MaxFigures);
}
