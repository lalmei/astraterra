using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace AstraTerra.Observation;

/// <summary>
/// Held-item observation state — the telescope scope, the sextant reading, the astrolabe planner —
/// lives in process-wide statics that drive the local player's HUD. Item interaction callbacks do
/// not respect that: they run on both sides, and for whichever entity is interacting. Everything
/// that touches those statics must pass through this gate first.
/// </summary>
public static class LocalObservationGate
{
    /// <summary>
    /// Whether an interaction should drive the local player's HUD.
    /// </summary>
    /// <remarks>
    /// Closes two separate leaks:
    /// <list type="bullet">
    /// <item>
    /// <b>Wrong side.</b> A player hosting a world runs a listen server inside their own client
    /// process, so a remote player's interaction executing server side would flip the statics the
    /// host's renderers read. A dedicated server hides this, because nothing in that process draws.
    /// </item>
    /// <item>
    /// <b>Wrong player.</b> Interaction callbacks can run for other players' entities on a client,
    /// so a nearby player raising a telescope would open the local overlay.
    /// </item>
    /// </list>
    /// </remarks>
    public static bool DrivesLocalHud(EnumAppSide side, string? interactingPlayerUid, string? localPlayerUid)
        => side == EnumAppSide.Client
           && !string.IsNullOrEmpty(interactingPlayerUid)
           && string.Equals(interactingPlayerUid, localPlayerUid, StringComparison.Ordinal);

    /// <summary>
    /// Resolves the side and player identities from an interacting entity, then applies
    /// <see cref="DrivesLocalHud"/>.
    /// </summary>
    public static bool IsLocalPlayerInteraction(EntityAgent? byEntity)
    {
        if (byEntity?.Api is not { } api)
        {
            return false;
        }

        var interactingPlayerUid = (byEntity as EntityPlayer)?.PlayerUID;
        var localPlayerUid = (api as ICoreClientAPI)?.World?.Player?.PlayerUID;
        return DrivesLocalHud(api.Side, interactingPlayerUid, localPlayerUid);
    }
}
