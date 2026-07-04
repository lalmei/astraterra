using AstraTerra.Observation;
using Vintagestory.API.Client;

namespace AstraTerra.Client.Zoom;

public static class TelescopeZoomHooks
{
    public static ICoreClientAPI? Api { get; private set; }

    public static void Initialize(ICoreClientAPI api)
    {
        Api = api;
    }

    public static void Reset()
    {
        Api = null;
    }

    public static float AdjustFov(float fov)
    {
        return TelescopeScopeState.IsScoped ? fov * TelescopeScopeState.GetFovMultiplier() : fov;
    }

    public static EnumCameraMode AdjustCameraMode(EnumCameraMode mode)
    {
        if (TelescopeScopeState.IsScoped)
        {
            return EnumCameraMode.FirstPerson;
        }

        return mode;
    }
}
