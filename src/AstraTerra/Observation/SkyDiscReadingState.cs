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

    /// <summary>
    /// How far the disc has come up towards the observer's eye, nought to one. An instrument like
    /// this is read at arm's length and raised to be read, so the hand follows the interaction
    /// rather than snapping to it.
    /// </summary>
    public static float RaiseFraction { get; private set; }

    /// <summary>What the last attempt to scratch a mark said, shown until the disc is lowered.</summary>
    public static string? LastMarkMessage { get; private set; }

    public static void Begin() => IsReading = true;

    public static void End()
    {
        IsReading = false;
        LastMarkMessage = null;
    }

    public static void ReportMark(string message) => LastMarkMessage = message;

    /// <summary>Eases the disc up while it is being read, and back down once it is lowered.</summary>
    public static void AdvanceRaise(float deltaTime)
    {
        const float SecondsToRaise = 0.25f;
        var step = float.IsFinite(deltaTime) ? Math.Clamp(deltaTime, 0f, 0.1f) / SecondsToRaise : 1f;
        RaiseFraction = Math.Clamp(RaiseFraction + (IsReading ? step : -step), 0f, 1f);
    }

    public static void Reset()
    {
        End();
        RaiseFraction = 0f;
    }
}
