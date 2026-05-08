using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.Client.NoObf;

namespace AstraTerra.Client.Zoom.Patches;

[HarmonyPatch]
public static class Set3DProjectionPatch
{
    [HarmonyPatch(typeof(ClientMain), "Set3DProjection", [typeof(float), typeof(float)])]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions)
    {
        return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TelescopeZoomHooks), nameof(TelescopeZoomHooks.AdjustFov))),
                new CodeInstruction(OpCodes.Starg_S, 2)
            }
            .Concat(instructions);
    }
}
