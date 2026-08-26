using AstraTerra.Items;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Observation;

/// <summary>
/// Keeps the local telescope overlay tied to the interaction that raised it.
/// </summary>
/// <remarks>
/// Vintage Story finishes a held interaction through whichever hotbar slot is active at that
/// moment, rather than the slot that began it. Changing slots while right mouse is held can
/// therefore bypass <see cref="ItemTelescope.OnHeldInteractCancel"/> and leave the process-wide
/// scope state active. The input and slot events are the client-side authority for repairing that
/// missed lifecycle callback.
/// </remarks>
public sealed class TelescopeScopeController
{
    private ICoreClientAPI? api;

    public void Start(ICoreClientAPI clientApi)
    {
        api = clientApi;
        TelescopeScopeState.End();
        clientApi.Event.MouseUp += OnMouseUp;
        clientApi.Event.AfterActiveSlotChanged += OnActiveSlotChanged;
        clientApi.Event.LeaveWorld += OnLeaveWorld;
    }

    public void Stop()
    {
        if (api is not null)
        {
            api.Event.MouseUp -= OnMouseUp;
            api.Event.AfterActiveSlotChanged -= OnActiveSlotChanged;
            api.Event.LeaveWorld -= OnLeaveWorld;
        }

        TelescopeScopeState.End();
        api = null;
    }

    private static void OnMouseUp(MouseEvent args)
    {
        if (args.Button == EnumMouseButton.Right)
        {
            TelescopeScopeState.End();
        }
    }

    private void OnActiveSlotChanged(ActiveSlotChangeEventArgs _)
    {
        var activeCollectible = api?.World?.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack?.Collectible;
        if (activeCollectible is not ItemTelescope)
        {
            TelescopeScopeState.End();
        }
    }

    private static void OnLeaveWorld()
        => TelescopeScopeState.End();
}
