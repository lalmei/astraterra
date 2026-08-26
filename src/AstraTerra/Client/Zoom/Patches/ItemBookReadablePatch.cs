using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Zoom.Patches;

/// <summary>
/// Protects vanilla's queued book interaction-help callback from an active-slot race.
/// </summary>
/// <remarks>
/// The hotbar queues help composition for the selected book, but the book's callback reads the
/// current active slot later. A second, quick slot change can leave that slot empty before
/// <c>ItemBook.isReadable</c> runs; vanilla 1.22.7 dereferences it without a null check.
/// </remarks>
[HarmonyPatch]
public static class ItemBookReadablePatch
{
    [HarmonyPrepare]
    public static bool Prepare()
        => AccessTools.TypeByName("Vintagestory.GameContent.ItemBook") is not null;

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => AccessTools.Method("Vintagestory.GameContent.ItemBook:isReadable");

    [HarmonyPrefix]
    public static bool Prefix(ItemSlot? slot, ref bool __result)
    {
        if (slot?.Itemstack?.Collectible?.Attributes is not null)
        {
            return true;
        }

        __result = false;
        return false;
    }
}
