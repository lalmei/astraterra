using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Items;

public sealed class ItemSextant : Item
{
    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SextantReadingState.Begin();
        }
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SextantReadingState.Begin();
        }
        return true;
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SextantReadingState.End();
        }
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason)
    {
        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SextantReadingState.End();
        }
        return true;
    }
}
