namespace AstraTerra.Observation;

/// <summary>
/// What the local player's astrolabe is doing while a plate is being cut, for the HUD to draw.
/// </summary>
/// <remarks>
/// Client-only and process-wide, like every other observation state here, so everything reaching it
/// must pass <see cref="LocalObservationGate"/> first. The authoritative cut happens on the server,
/// which writes the itemstack attribute; this is only the progress bar and the reason the sighting
/// is refused, both of which exist to be looked at rather than trusted.
/// </remarks>
public static class AstrolabeCalibrationState
{
    public static bool IsCalibrating { get; private set; }

    /// <summary>0 to 1 across <see cref="AstrolabeCalibrationPolicy.CalibrationSeconds"/>.</summary>
    public static float Progress { get; private set; }

    /// <summary>Why the sighting cannot be taken from here, or null while it can.</summary>
    public static string? Obstacle { get; private set; }

    public static void Update(float secondsUsed, string? obstacle)
    {
        IsCalibrating = true;
        Obstacle = obstacle;

        // A blocked sighting shows an empty bar rather than a frozen one: the seconds keep running
        // while the button is held, and a bar stuck at 40% reads as a bug rather than as a refusal.
        Progress = obstacle is null ? AstrolabeCalibrationPolicy.Progress(secondsUsed) : 0f;
    }

    public static void End()
    {
        IsCalibrating = false;
        Progress = 0f;
        Obstacle = null;
    }

    public static void Reset()
        => End();
}
