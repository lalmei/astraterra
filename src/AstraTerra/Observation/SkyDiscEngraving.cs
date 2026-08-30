using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>
/// How many of the observer's own constellations a disc will carry.
/// </summary>
/// <remarks>
/// One figure, whatever it is made of: a disc is one object with one face, and what goes on it is
/// the figure its owner thought worth carrying rather than a notebook page of them.
/// <para>
/// Holding a figure and being able to take one are different questions, and clay is why. A figure
/// is pressed into a clay disc while the clay is still soft and is fixed by the firing; a fired
/// disc carries the figure it was fired with and will never take another. Metal is worked cold and
/// can be engraved whenever its owner likes.
/// </para>
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

    /// <summary>
    /// Whether this disc can be worked now, as opposed to whether it can carry a figure at all.
    /// </summary>
    /// <remarks>
    /// False for fired clay, which holds the figure it was fired with and takes no more.
    /// </remarks>
    public static bool IsWorkable(ItemStack? stack)
        => stack?.Collectible?.Attributes?["engravable"].AsBool(false) ?? false;
}
