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

    /// <summary>
    /// How far the raised disc has been turned in the hand, clockwise from the way it came up.
    /// </summary>
    /// <remarks>
    /// A disc is a thing you turn: you bring a mark round to where you are looking rather than
    /// walking round the disc. This is how it is being held, not what it says — the marks on it are
    /// bearings and mean the same whichever way up it is read.
    /// </remarks>
    public static float FaceTurnDeg { get; private set; }

    /// <summary>How far one notch of the wheel turns the disc.</summary>
    /// <remarks>
    /// A twelfth of a turn: coarse enough to bring a mark round in a moment, and fine enough that
    /// the graduations do not jump past the eye.
    /// </remarks>
    public const float TurnStepDeg = 15f;

    /// <summary>
    /// Set only while the raised face is the thing being drawn. Every disc in the game shares one
    /// item and one GUI transform, so without this the turn would be applied to the hotbar icon and
    /// to every disc in an open inventory as well.
    /// </summary>
    public static bool IsDrawingRaisedFace { get; private set; }

    public static void Begin()
    {
        // A disc picked up again comes up the way it was made, not the way it was last left.
        if (!IsReading)
        {
            FaceTurnDeg = 0f;
        }

        IsReading = true;
    }

    public static void End()
    {
        IsReading = false;
        LastMarkMessage = null;
    }

    /// <summary>Turns the raised disc by whole notches of the wheel, wrapping at a full circle.</summary>
    public static void Turn(int notches)
    {
        if (!IsReading || notches == 0)
        {
            return;
        }

        var turned = (FaceTurnDeg + (notches * TurnStepDeg)) % 360f;
        FaceTurnDeg = turned < 0f ? turned + 360f : turned;
    }

    public static void BeginDrawingRaisedFace() => IsDrawingRaisedFace = true;

    public static void EndDrawingRaisedFace() => IsDrawingRaisedFace = false;

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
        FaceTurnDeg = 0f;
        IsDrawingRaisedFace = false;
    }
}
