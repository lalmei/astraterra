using System.Reflection;
using AstraTerra.Items;
using AstraTerra.Observation;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Keeps a disc's marks on it once it has been set down.
/// </summary>
/// <remarks>
/// Ground storage draws an item from the one model the game keeps per item, which is the right call
/// for a plate of copper and the wrong one for an object whose whole content is on the itemstack: a
/// disc lying on the floor would come out blank, and the marks would appear again the moment it was
/// picked up. So the mesh it chose is swapped for the one this stack actually looks like.
/// <para>
/// Done by patch rather than by interface because ground storage offers no hook for items, and by
/// name rather than by reference so that a game without that class — or with it moved — leaves the
/// disc drawn plainly instead of failing to load.
/// </para>
/// </remarks>
public sealed class GroundStorageDiscMeshPatcher
{
    private const string GroundStorageType = "Vintagestory.GameContent.BlockEntityGroundStorage";
    private const string MeshMethod = "getOrCreateMesh";
    private const string MeshRefsField = "MeshRefs";

    private readonly Harmony harmony = new("astraterra.grounddiscmesh");
    private bool patched;

    public bool Start(ICoreClientAPI api)
    {
        var target = AccessTools.Method(AccessTools.TypeByName(GroundStorageType), MeshMethod);
        if (target is null)
        {
            api.Logger.Notification(
                "AstraTerra found no ground storage mesh to hook; a sky disc set down will show no marks.");
            return false;
        }

        harmony.Patch(
            target,
            postfix: new HarmonyMethod(
                typeof(GroundStorageDiscMeshPatcher).GetMethod(
                    nameof(ShowTheMarks),
                    BindingFlags.NonPublic | BindingFlags.Static)));

        patched = true;
        return true;
    }

    public void Stop()
    {
        if (patched)
        {
            harmony.UnpatchAll(harmony.Id);
            patched = false;
        }
    }

    private static void ShowTheMarks(object __instance, ItemSlot slot, int index)
    {
        if (slot?.Itemstack?.Collectible is not ItemSkyDisc disc)
        {
            return;
        }

        if (SkyDiscMeshes.Active?.Get(disc, SolarBandStore.Read(slot.Itemstack)) is not { } marked)
        {
            return;
        }

        if (AccessTools.Field(__instance.GetType(), MeshRefsField)?.GetValue(__instance)
            is not MultiTextureMeshRef[] meshRefs)
        {
            return;
        }

        if (index >= 0 && index < meshRefs.Length)
        {
            // The cache owns this model and outlives the block: ground storage only ever borrows the
            // model it was handed here, exactly as it borrows the game's own.
            meshRefs[index] = marked;
        }
    }
}
