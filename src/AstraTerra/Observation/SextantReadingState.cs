namespace AstraTerra.Observation;

public static class SextantReadingState
{
    public static bool IsReading { get; private set; }

    public static void Begin()
    {
        IsReading = true;
    }

    public static void End()
    {
        IsReading = false;
    }
}
