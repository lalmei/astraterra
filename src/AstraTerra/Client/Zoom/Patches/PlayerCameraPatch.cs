using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.Client.NoObf;

namespace AstraTerra.Client.Zoom.Patches;

[HarmonyPatch]
public static class PlayerCameraPatch
{
    [HarmonyPatch(typeof(PlayerCamera), "OnBeforeRenderFrame3D")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        var cameraModeField = typeof(Camera).GetField("CameraMode", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Could not locate PlayerCamera.CameraMode field.");

        return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Dup),
                new CodeInstruction(OpCodes.Ldfld, cameraModeField),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TelescopeZoomHooks), nameof(TelescopeZoomHooks.AdjustCameraMode))),
                new CodeInstruction(OpCodes.Stfld, cameraModeField)
            }
            .Concat(instructions);
    }
}
