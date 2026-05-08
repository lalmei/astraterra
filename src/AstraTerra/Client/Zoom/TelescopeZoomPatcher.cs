using HarmonyLib;
using Vintagestory.API.Client;

namespace AstraTerra.Client.Zoom;

public sealed class TelescopeZoomPatcher
{
    private readonly Harmony harmony = new("astraterra.telescopezoom");

    public void Start(ICoreClientAPI api)
    {
        TelescopeZoomHooks.Initialize(api);
        harmony.PatchAll(typeof(TelescopeZoomPatcher).Assembly);
    }

    public void Stop()
    {
        harmony.UnpatchAll(harmony.Id);
    }
}
