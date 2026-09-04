using System.Runtime.CompilerServices;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

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
/// The carry is put back on before <c>KillFire</c> rather than after <c>OnFired</c> returns. The
/// kiln does not survive its own firing: <c>KillFire</c> puts a ground storage block where the kiln
/// was, hands the new block entity the kiln's slots and tells the world about it. Anything written
/// after that is written to a block entity that is no longer in the world and announced to nobody,
/// so the disc a player digs out is the blank one they were already sent. Writing first means the
/// hand-off carries the figure with it. <c>OnFired</c>'s own postfix stays as a second pass, for a
/// kiln that somehow fires without dying.
/// </para>
/// <para>
/// Patched by hand rather than by attribute so that the client's assembly-wide <c>PatchAll</c> does
/// not claim it: firing happens on the server, this has to be applied on both sides for single
/// player and for a dedicated server alike, and being applied twice would be the client and the
/// server each copying the same attributes over each other.
/// </para>
/// </remarks>
public sealed class PitKilnFiringKeepsTheDiscPatch
{
    /// <summary>The attributes a disc is worth keeping for, in the order the kiln holds its slots.</summary>
    private static readonly string[] CarriedAttributes =
    [
        SkyDiscFigureStore.FigureJsonAttribute,
        SolarBandStore.BandJsonAttribute,
    ];

    /// <summary>
    /// What each kiln that is part way through firing held before it fired.
    /// </summary>
    /// <remarks>
    /// A field on the patch rather than Harmony's own <c>__state</c>, because the restore happens
    /// inside a second method — <c>KillFire</c>, which the firing calls — and the two patches have
    /// no other way to speak to each other. Weak, so that a kiln in an unloaded chunk that never
    /// finishes is not held in memory by this.
    /// </remarks>
    private static readonly ConditionalWeakTable<object, Dictionary<int, Dictionary<string, string>>> Pending = new();

    private readonly Harmony harmony = new("astraterra.skydiscfiring");
    private bool applied;

    /// <summary>Whether the game still has the kiln this patch is about.</summary>
    public static bool IsAvailable => FiredMethod is not null && KillFireMethod is not null;

    /// <summary>
    /// The firing itself, and the death of the fire that ends it.
    /// </summary>
    /// <remarks>
    /// Taken off the type rather than looked up by name. The mod compiles against
    /// <c>BlockEntityPitKiln</c>, so the type is in hand already, and a name lookup only adds a way
    /// for this to quietly resolve to nothing on a game whose survival mod was loaded somewhere
    /// Harmony does not think to look.
    /// </remarks>
    private static System.Reflection.MethodInfo? FiredMethod
        => AccessTools.Method(typeof(BlockEntityPitKiln), nameof(BlockEntityPitKiln.OnFired));

    private static System.Reflection.MethodInfo? KillFireMethod
        => AccessTools.Method(typeof(BlockEntityPitKiln), nameof(BlockEntityPitKiln.KillFire));

    public bool Start(ICoreAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        if (applied)
        {
            return true;
        }

        if (FiredMethod is not { } fired || KillFireMethod is not { } killFire)
        {
            api.Logger.Warning(
                "AstraTerra could not find BlockEntityPitKiln.OnFired/KillFire; a figure drawn on a clay "
                + "disc will not survive firing.");
            return false;
        }

        harmony.Patch(
            fired,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PitKilnFiringKeepsTheDiscPatch), nameof(FiredPrefix))),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(PitKilnFiringKeepsTheDiscPatch), nameof(FiredPostfix))));

        harmony.Patch(
            killFire,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(PitKilnFiringKeepsTheDiscPatch), nameof(KillFirePrefix))));

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
    public static void FiredPrefix(object __instance)
    {
        if (Capture(__instance) is { Count: > 0 } kept)
        {
            Pending.AddOrUpdate(__instance, kept);
        }
    }

    /// <summary>
    /// Puts it back while the kiln still owns its slots, which is the moment before it hands them to
    /// the ground storage that replaces it.
    /// </summary>
    public static void KillFirePrefix(object __instance) => Restore(__instance);

    /// <summary>The second pass, and where the kiln stops being part way through anything.</summary>
    public static void FiredPostfix(object __instance)
    {
        Restore(__instance);
        Pending.Remove(__instance);
    }

    private static Dictionary<int, Dictionary<string, string>>? Capture(object instance)
    {
        var inventory = (instance as IBlockEntityContainer)?.Inventory;
        if (inventory is null)
        {
            return null;
        }

        Dictionary<int, Dictionary<string, string>>? captured = null;
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
                captured ??= [];
                captured[index] = kept;
            }
        }

        return captured;
    }

    private static void Restore(object instance)
    {
        if (!Pending.TryGetValue(instance, out var state))
        {
            return;
        }

        var inventory = (instance as IBlockEntityContainer)?.Inventory;
        if (inventory is null)
        {
            return;
        }

        var restored = false;
        foreach (var (index, kept) in state)
        {
            if (index >= inventory.Count)
            {
                continue;
            }

            var slot = inventory[index];

            // Only onto a disc. If the kiln turned it into something else entirely, another mod has
            // changed what firing a disc means and this has no business writing on the result. And
            // only onto a disc that is still blank: on the second pass the figure is already there.
            if (slot?.Itemstack is not { } fired || !SkyDiscHeld.IsDisc(fired))
            {
                continue;
            }

            foreach (var (attribute, value) in kept)
            {
                if (fired.Attributes.GetString(attribute, null) is { Length: > 0 })
                {
                    continue;
                }

                fired.Attributes.SetString(attribute, value);
                restored = true;
            }

            slot.MarkDirty();
        }

        if (restored)
        {
            (instance as BlockEntity)?.MarkDirty(true);
        }
    }
}
