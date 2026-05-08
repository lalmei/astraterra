namespace AstraTerra.Client.Observation;

public static class TelescopeObservationState
{
    public const int MinZoomStep = 1;
    public const int MaxZoomStep = 5;
    public const int PrecisionMaxZoomStep = 10;

    private const float WideFovMultiplier = 0.45f;
    public const float MaxZoomFovMultiplier = 0.12f;
    public const float PrecisionMaxZoomFovMultiplier = 0.06f;

    public static ObservationMode NextMode(ObservationMode current) => current switch
    {
        ObservationMode.Observe => ObservationMode.Draw,
        ObservationMode.Draw => ObservationMode.Inspect,
        ObservationMode.Inspect => ObservationMode.RemoveSegment,
        _ => ObservationMode.Observe
    };

    public static int ClampZoomStep(int zoomStep) => ClampZoomStep(zoomStep, MaxZoomStep);

    public static int ClampZoomStep(int zoomStep, int maxZoomStep)
        => Math.Clamp(zoomStep, MinZoomStep, Math.Max(MinZoomStep, maxZoomStep));

    public static float GetFovMultiplier(int zoomStep, int maxZoomStep)
        => GetFovMultiplier(zoomStep, maxZoomStep, MaxZoomFovMultiplier);

    public static float GetFovMultiplier(int zoomStep, int maxZoomStep, float maxZoomFovMultiplier)
    {
        var normalizedMaxZoomStep = Math.Max(MinZoomStep, maxZoomStep);
        var normalizedZoomStep = ClampZoomStep(zoomStep, normalizedMaxZoomStep);
        var normalizedMaxZoomFovMultiplier = Math.Clamp(maxZoomFovMultiplier, 0.01f, WideFovMultiplier);
        if (normalizedMaxZoomStep == MaxZoomStep && Math.Abs(normalizedMaxZoomFovMultiplier - MaxZoomFovMultiplier) < 0.0001f)
        {
            return normalizedZoomStep switch
            {
                <= 1 => 0.45f,
                2 => 0.35f,
                3 => 0.25f,
                4 => 0.18f,
                _ => MaxZoomFovMultiplier
            };
        }

        var zoomProgress = normalizedMaxZoomStep == MinZoomStep
            ? 1.0f
            : (normalizedZoomStep - MinZoomStep) / (float)(normalizedMaxZoomStep - MinZoomStep);
        return WideFovMultiplier + ((normalizedMaxZoomFovMultiplier - WideFovMultiplier) * zoomProgress);
    }
}
