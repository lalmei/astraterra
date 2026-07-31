using System.Reflection;
using HarmonyLib;
using Vintagestory.Client.NoObf;

namespace AstraTerra.Client.Rendering;

[HarmonyPatch]
public static class VanillaNightSkyRendererPatch
{
    private const string VanillaRendererTypeName = "Vintagestory.Client.NoObf.SystemRenderNightSky";

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
        var rendererType = typeof(SystemRenderSunMoon).Assembly.GetType(VanillaRendererTypeName)
            ?? throw new MissingMemberException($"Could not find {VanillaRendererTypeName}.");

        return rendererType.GetMethod(
                "OnRenderFrame3D",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(float)],
                modifiers: null)
            ?? throw new MissingMethodException(VanillaRendererTypeName, "OnRenderFrame3D");
    }

    [HarmonyPrefix]
    public static bool Prefix()
        => SkyStarSunMoonRenderer.ShouldRenderVanillaStarfield;
}
