namespace AstraTerra.Observation;

public static class AstrolabeReadingState
{
    public static bool IsReading { get; private set; }

    public static int SelectedTargetIndex { get; private set; }

    public static double ForecastHours { get; private set; }

    public static void Begin()
    {
        IsReading = true;
    }

    public static void End()
    {
        IsReading = false;
        ForecastHours = 0;
    }

    public static void Reset()
    {
        IsReading = false;
        SelectedTargetIndex = 0;
        ForecastHours = 0;
    }

    public static void CycleTarget(int targetCount)
    {
        SelectedTargetIndex = targetCount <= 0
            ? 0
            : (SelectedTargetIndex + 1) % targetCount;
    }

    public static int NormalizeTargetIndex(int targetCount)
    {
        if (targetCount <= 0)
        {
            SelectedTargetIndex = 0;
            return 0;
        }

        SelectedTargetIndex %= targetCount;
        return SelectedTargetIndex;
    }

    public static void AdjustForecastHours(double deltaHours, double maximumHours)
    {
        ForecastHours = Math.Clamp(ForecastHours + deltaHours, 0, Math.Max(0, maximumHours));
    }
}
