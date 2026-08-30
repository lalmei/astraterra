using AstraTerra.Constellations;
using AstraTerra.Observation;
using HarmonyLib;
using Vintagestory.API.Common;

namespace AstraTerra.Items.Patches;

/// <summary>
/// Carries what was worked into a clay disc through the firing that hardens it.
/// </summary>
/// <remarks>
/// A figure goes into a clay disc while the clay is soft, and the firing is what fixes it. But a pit
/// kiln does not transform the object in it — <c>BlockEntityPitKiln.OnFired</c> clones the fired
/// item out of the recipe and drops the raw one, attributes and all. Without this, a disc drawn on
/// all evening comes out of the kiln blank, which reads as the game having eaten the work.
/// <para>
/// Patched by hand rather than by attribute so that the client's assembly-wide <c>PatchAll</c> does
/// not claim it: firing happens on the server, this has to be applied on both sides for single
/// player and for a dedicated server alike, and being applied twice would be the client and the
/// server each copying the same attributes over each other.
/// </para>
/// </remarks>
public sealed class PitKilnFiringKeepsTheDiscPatch
{
    private const string TargetType = "Vintagestory.GameContent.BlockEntityPitKiln";
    private const string TargetMethod = "OnFired";

    /// <summary>The attributes a disc is worth keeping for, in the order the kiln holds its slots.</summary>
    private static readonly string[] CarriedAttributes =
    [
        SkyDiscFigureStore.FigureJsonAttribute,
        SolarBandStore.BandJsonAttribute,
    ];

    private readonly Harmony harmony = new("astraterra.skydiscfiring");
    private bool applied;

    /// <summary>Whether the game still has the kiln this patch is about.</summary>
    public static bool IsAvailable => AccessTools.Method($"{TargetType}:{TargetMethod}") is not null;

    public bool Start(ICoreAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        if (applied)
        {
            return true;
        }

        var target = AccessTools.Method($"{TargetType}:{TargetMethod}");
        if (target is null)
        {
            api.Logger.Warning(
                "AstraTerra could not find {0}:{1}; a figure drawn on a clay disc will not survive firing.",
                TargetType,
                TargetMethod);
            return false;
        }

        harmony.Patch(
            target,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PitKilnFiringKeepsTheDiscPatch), nameof(Prefix))),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PitKilnFiringKeepsTheDiscPatch), nameof(Postfix))));
        applied = true;
        return true;
    }

    public void Stop()
    {
        if (applied)
        {
            harmony.UnpatchAll(harmony.Id);
            applied = false;
        }
    }

    /// <summary>What the kiln held before it fired, by slot.</summary>
    public static void Prefix(object __instance, out Dictionary<int, Dictionary<string, string>>? __state)
    {
        __state = null;
        var inventory = (__instance as IBlockEntityContainer)?.Inventory;
        if (inventory is null)
        {
            return;
        }

        for (var index = 0; index < inventory.Count; index++)
        {
            var stack = inventory[index]?.Itemstack;
            if (!SkyDiscHeld.IsDisc(stack))
            {
                continue;
            }

            var kept = new Dictionary<string, string>();
            foreach (var attribute in CarriedAttributes)
            {
                if (stack!.Attributes?.GetString(attribute, null) is { Length: > 0 } value)
                {
                    kept[attribute] = value;
                }
            }

            if (kept.Count > 0)
            {
                __state ??= [];
                __state[index] = kept;
            }
        }
    }

    /// <summary>Puts it back on whatever came out of that same slot.</summary>
    public static void Postfix(object __instance, Dictionary<int, Dictionary<string, string>>? __state)
    {
        if (__state is null)
        {
            return;
        }

        var inventory = (__instance as IBlockEntityContainer)?.Inventory;
        if (inventory is null)
        {
            return;
        }

        var restored = false;
        foreach (var (index, kept) in __state)
        {
            if (index >= inventory.Count)
            {
                continue;
            }

            var slot = inventory[index];

            // Only onto a disc. If the kiln turned it into something else entirely, another mod has
            // changed what firing a disc means and this has no business writing on the result.
            if (slot?.Itemstack is not { } fired || !SkyDiscHeld.IsDisc(fired))
            {
                continue;
            }

            foreach (var (attribute, value) in kept)
            {
                fired.Attributes.SetString(attribute, value);
            }

            slot.MarkDirty();
            restored = true;
        }

        if (restored)
        {
            (__instance as BlockEntity)?.MarkDirty(true);
        }
    }
}
