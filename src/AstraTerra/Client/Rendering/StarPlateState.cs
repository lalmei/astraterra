using Vintagestory.API.Client;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Whether the local player has their journal open at a plate, and which one.
/// </summary>
/// <remarks>
/// Client-only and holder-only, the same shape as <see cref="AstraTerra.Observation.SkyDiscReadingState"/>:
/// a page is a HUD, and nobody but the reader is looking at it.
/// </remarks>
public static class StarPlateState
{
    public const string HotkeyCode = "astraterra-starplate";

    /// <summary>
    /// Vanilla leaves B alone, and a book is what it opens. Remappable under Controls like any other.
    /// </summary>
    public const GlKeys DefaultKey = GlKeys.B;

    public static bool IsOpen { get; private set; }

    /// <summary>Which plate is face up, counted from the first.</summary>
    /// <remarks>
    /// Kept as a plain index rather than a constellation id: a plate is a place in a book, and a
    /// book that gains a figure while shut should open where it was left, not hunt for a record.
    /// The renderer clamps this against however many plates the held book actually has, because the
    /// book in hand can change between one frame and the next.
    /// </remarks>
    public static int PageIndex { get; private set; }

    public static void Open()
    {
        IsOpen = true;
    }

    public static void Close()
    {
        IsOpen = false;
    }

    /// <summary>Opens the book, or shuts it if it is already open.</summary>
    public static bool Toggle()
    {
        if (IsOpen)
        {
            Close();
            return false;
        }

        Open();
        return true;
    }

    /// <summary>
    /// Turns whole plates, stopping at the covers.
    /// </summary>
    /// <remarks>
    /// It does not wrap. A book you can page past the end of gives you no way to tell the last plate
    /// from the first, and the count is the one thing a reader is entitled to feel.
    /// </remarks>
    public static void TurnPage(int plates, int pageCount)
    {
        if (!IsOpen || plates == 0 || pageCount <= 0)
        {
            return;
        }

        PageIndex = Math.Clamp(PageIndex + plates, 0, pageCount - 1);
    }

    /// <summary>Keeps the open plate inside a book that has since changed in the reader's hand.</summary>
    public static int ClampPage(int pageCount)
    {
        if (pageCount <= 0)
        {
            PageIndex = 0;
            return 0;
        }

        PageIndex = Math.Clamp(PageIndex, 0, pageCount - 1);
        return PageIndex;
    }

    public static void Reset()
    {
        IsOpen = false;
        PageIndex = 0;
    }
}
