namespace AstraTerra.Observation;

/// <summary>
/// Local first-person lie-down pose. Like telescope scope state, this is process-wide and must
/// only be flipped for the local player — a hosted world runs its server in the same process.
/// </summary>
public static class SkyLyingState
{
    public static bool IsLying { get; private set; }

    public static void Begin()
    {
        IsLying = true;
    }

    public static void End()
    {
        IsLying = false;
    }

    public static void Reset()
    {
        IsLying = false;
    }
}
