using AstraTerra.Client.Observation;

namespace AstraTerra.Items;

public sealed class ItemPrecisionTelescope : ItemTelescope
{
    protected override int MaxZoomStep => TelescopeObservationState.PrecisionMaxZoomStep;
    protected override float MaxZoomFovMultiplier => TelescopeObservationState.PrecisionMaxZoomFovMultiplier;
}
