using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Items;

public sealed class ItemAstrolabe : Item
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
        AstrolabeReadingState.Begin();
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        AstrolabeReadingState.Begin();
        return true;
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        AstrolabeReadingState.End();
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason)
    {
        AstrolabeReadingState.End();
        return true;
    }
}
