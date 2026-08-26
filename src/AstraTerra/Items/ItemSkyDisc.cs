using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Observation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Items;

/// <summary>
/// A disc of bronze with two rims. Right click reads its face; sneak and right click scratches
/// where the sun crossed the horizon today.
/// </summary>
/// <remarks>
/// The cheapest instrument in the mod and the slowest: it reads no angles, needs no literacy, no ink
/// and no glass, and it pays out a latitude, the length of the year, and the date the seasons turn —
/// over a world year of evenings. Everything the astrolabe answers in three seconds, bought with
/// patience instead.
/// </remarks>
public sealed class ItemSkyDisc : Item
{
    private const string MarkingAttribute = "astraterraDiscMarking";

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        // Sneaking at the ground sets the disc down; sneaking at the sky scratches it. Ground
        // storage is a collectible behaviour, and behaviours only ever run through the base call,
        // so hand the click over before claiming it. Aiming at the horizon selects no block, which
        // is where marking happens anyway.
        if (byEntity?.Controls?.Sneak == true && blockSel?.Face == BlockFacing.UP)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling != EnumHandHandling.NotHandled)
            {
                return;
            }
        }

        handling = EnumHandHandling.PreventDefault;

        // Decided once at the start, for the reason ItemAstrolabe spells out: an Item is one shared
        // instance, and letting go of sneak halfway must not turn a mark into a reading.
        var marking = byEntity?.Controls?.Sneak == true;
        byEntity?.Attributes?.SetBool(MarkingAttribute, marking);

        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SkyDiscReadingState.Begin();
        }

        if (marking && byEntity is not null)
        {
            Scratch(slot, byEntity);
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
            SkyDiscReadingState.Begin();
        }

        return true;
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
        => Finish(byEntity);

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason)
    {
        Finish(byEntity);
        return true;
    }

    /// <summary>
    /// Scratches today's mark, if the sun is within a window of the horizon and the disc will take it.
    /// </summary>
    /// <remarks>
    /// The server owns the itemstack, so the server owns the rim. The client runs the same check only
    /// to say something immediately — a scratch that the server refuses is refused on both sides,
    /// because both are reading the same sun.
    /// </remarks>
    private static void Scratch(ItemSlot slot, EntityAgent byEntity)
    {
        var world = byEntity.World;
        if (world is null || slot?.Itemstack is null)
        {
            return;
        }

        var isLocal = LocalObservationGate.IsLocalPlayerInteraction(byEntity);
        if (!SkyExposure.CanSeeSky(world.BlockAccessor, byEntity))
        {
            Report(isLocal, "No horizon from in here.");
            return;
        }

        var calendar = world.Calendar;
        var hoursPerDay = Math.Max(1.0, calendar.HoursPerDay);
        var position = byEntity.Pos.XYZ;
        var crossing = SolarHorizonCrossing.FindNearest(
            calendar.TotalDays,
            hoursPerDay,
            at =>
            {
                var vector = calendar.GetSunPosition(position, at);
                var sun = SkyBodyModel.FromWorldDirection("Sun", vector.X, vector.Y, vector.Z);
                return sun is null ? (double.NaN, double.NaN) : (sun.AltitudeDeg, sun.AzimuthDeg);
            });

        if (crossing is null)
        {
            Report(isLocal, "The sun is nowhere near the horizon. Come back at sunrise or sunset.");
            return;
        }

        var latitude = LatitudeMapper.MapGameLatitude(
            byEntity.Pos.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var band = SolarBandStore.ReadOrEmpty(slot.Itemstack);
        var result = band.Scratch(
            (int)Math.Floor(crossing.TotalDays),
            crossing.AzimuthDeg,
            crossing.Event,
            latitude,
            (int)Math.Floor(byEntity.Pos.X),
            (int)Math.Floor(byEntity.Pos.Z));

        Report(isLocal, result.Message);
        if (result.Outcome != SolarMarkOutcome.Scratched || world.Side != EnumAppSide.Server)
        {
            return;
        }

        SolarBandStore.Write(slot.Itemstack, band);

        // Without this the scratch exists only in the server's copy, and the face that reads it is
        // on the client.
        slot.MarkDirty();
    }

    private static void Report(bool isLocal, string message)
    {
        if (isLocal)
        {
            SkyDiscReadingState.ReportMark(message);
        }
    }

    private static void Finish(EntityAgent? byEntity)
    {
        byEntity?.Attributes?.RemoveAttribute(MarkingAttribute);

        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SkyDiscReadingState.End();
        }
    }
}
