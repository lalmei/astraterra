using AstraTerra.Client.Observation;

namespace AstraTerra.Observation;

public static class TelescopeScopeState
{
    public static bool IsScoped { get; private set; }
    public static int ZoomStep { get; private set; } = 3;
    public static int MaxZoomStep { get; private set; } = TelescopeObservationState.MaxZoomStep;
    public static float MaxZoomFovMultiplier { get; private set; } = TelescopeObservationState.MaxZoomFovMultiplier;
    public static ObservationMode Mode { get; private set; } = ObservationMode.Observe;

    public static void Begin(
        int maxZoomStep = TelescopeObservationState.MaxZoomStep,
        float maxZoomFovMultiplier = TelescopeObservationState.MaxZoomFovMultiplier)
    {
        MaxZoomStep = Math.Max(TelescopeObservationState.MinZoomStep, maxZoomStep);
        MaxZoomFovMultiplier = Math.Clamp(maxZoomFovMultiplier, 0.01f, 0.45f);
        ZoomStep = TelescopeObservationState.ClampZoomStep(ZoomStep, MaxZoomStep);
        IsScoped = true;
    }

    public static void End()
    {
        IsScoped = false;
    }

    public static void CycleMode()
    {
        Mode = TelescopeObservationState.NextMode(Mode);
    }

    public static void ScrollZoom(int deltaSteps)
    {
        ZoomStep = TelescopeObservationState.ClampZoomStep(ZoomStep + deltaSteps, MaxZoomStep);
    }

    public static float GetFovMultiplier()
    {
        return TelescopeObservationState.GetFovMultiplier(ZoomStep, MaxZoomStep, MaxZoomFovMultiplier);
    }
}
