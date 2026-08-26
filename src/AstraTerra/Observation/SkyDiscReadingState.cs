namespace AstraTerra.Observation;

/// <summary>
/// Whether the local player is holding the disc up to read its face.
/// </summary>
/// <remarks>
/// Client-only and holder-only, the same shape as <see cref="SextantReadingState"/>: the disc's face
/// is a HUD, and nobody but its owner is looking at it.
/// </remarks>
public static class SkyDiscReadingState
{
    public static bool IsReading { get; private set; }

    /// <summary>What the last attempt to scratch a mark said, shown until the disc is lowered.</summary>
    public static string? LastMarkMessage { get; private set; }

    public static void Begin() => IsReading = true;

    public static void End()
    {
        IsReading = false;
        LastMarkMessage = null;
    }

    public static void ReportMark(string message) => LastMarkMessage = message;

    public static void Reset() => End();
}
