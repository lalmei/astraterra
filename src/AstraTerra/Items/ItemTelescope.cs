using AstraTerra.Client.Observation;
using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Items;

public abstract class ItemTelescope : Item
{
    protected virtual int MaxZoomStep => TelescopeObservationState.MaxZoomStep;
    protected virtual float MaxZoomFovMultiplier => TelescopeObservationState.MaxZoomFovMultiplier;

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
        TelescopeScopeState.Begin(MaxZoomStep, MaxZoomFovMultiplier);
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        TelescopeScopeState.Begin(MaxZoomStep, MaxZoomFovMultiplier);
        return true;
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        TelescopeScopeState.End();
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason)
    {
        TelescopeScopeState.End();
        return true;
    }
}
