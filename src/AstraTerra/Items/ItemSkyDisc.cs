using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

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
public sealed class ItemSkyDisc : Item, IContainedMeshSource
{
    private const string MarkingAttribute = "astraterraDiscMarking";
    private const string ScratchedAttribute = "astraterraDiscScratched";

    /// <summary>
    /// How long the disc is held to the horizon before the scratch goes on. A mark cannot be taken
    /// off again, so it is worth a moment's deliberation — and the moment is what the gesture is:
    /// the disc comes up, is lined up against where the sun went down, and only then is scratched.
    /// </summary>
    private const float ScratchHoldSeconds = 1.2f;

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

        byEntity?.Attributes?.SetBool(ScratchedAttribute, false);
        if (marking && byEntity is not null)
        {
            Preflight(slot, byEntity);
        }
    }

    /// <summary>
    /// The disc as it is drawn where it lies: on the ground, on a shelf, in a display case.
    /// </summary>
    /// <remarks>
    /// The supported way for an itemstack to be drawn as more than its item's one model. Nothing
    /// back means an unscratched disc, and the game falls back to the model it already has. This
    /// runs on the threads that build chunk meshes, so it tesselates and never uploads.
    /// </remarks>
    public MeshData GenMesh(ItemSlot slot, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
        => SkyDiscMeshes.Active?.BuildDisplayMesh(this, SolarBandStore.Read(slot?.Itemstack), targetAtlas)!;

    /// <summary>
    /// What this disc looks like, as a name the game can cache a model against. Every disc of a
    /// material would otherwise share one cached model, and the first one drawn would lend its marks
    /// to every other.
    /// </summary>
    public string GetMeshCacheKey(ItemSlot slot)
        => SkyDiscMeshes.CacheKey(this, SolarBandStore.Read(slot?.Itemstack));

    /// <summary>
    /// Draws this disc with its own marks on it, wherever it is being shown.
    /// </summary>
    /// <remarks>
    /// The band belongs to the itemstack, so two discs are two different objects: the model has to
    /// come from the stack rather than from the item. An unscratched disc falls through to the
    /// shipped model, which is what it looks like.
    /// </remarks>
    public override void OnBeforeRender(
        ICoreClientAPI capi,
        ItemStack itemstack,
        EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

        if (SkyDiscMeshes.Active?.Get(this, SolarBandStore.Read(itemstack)) is { } marked)
        {
            renderinfo.ModelRef = marked;
        }

        // One target covers the held item in both views now; first person is where it is felt.
        if (target == EnumItemRenderTarget.HandTp)
        {
            Raise(ref renderinfo);
        }
    }

    /// <summary>
    /// Brings the disc up in front of its holder while they are reading or marking it, and lets it
    /// back down when they lower it.
    /// </summary>
    /// <remarks>
    /// You do not read an instrument like this at your hip. The transform is copied before it is
    /// changed: the one on the item is shared by every disc in the world, and moving it would raise
    /// all of them for good.
    /// </remarks>
    private static void Raise(ref ItemRenderInfo renderinfo)
    {
        SkyDiscReadingState.AdvanceRaise(renderinfo.dt);

        var raised = SkyDiscReadingState.RaiseFraction;
        if (raised <= 0f || renderinfo.Transform is null)
        {
            return;
        }

        // Eased so the disc comes up and settles rather than snapping to the eye.
        var amount = raised * raised * (3f - (2f * raised));
        var transform = renderinfo.Transform.Clone();
        transform.Translation.X += 0.28f * amount;
        transform.Translation.Y += 0.16f * amount;
        transform.Translation.Z -= 0.12f * amount;
        transform.Rotation.Z -= 22f * amount;
        renderinfo.Transform = transform;
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

        if (byEntity?.Attributes?.GetBool(MarkingAttribute) == true
            && byEntity.Attributes.GetBool(ScratchedAttribute) != true
            && secondsUsed >= ScratchHoldSeconds)
        {
            byEntity.Attributes.SetBool(ScratchedAttribute, true);
            Scratch(slot, byEntity);
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
    /// Says what the disc is about to be scratched with, before the hold that scratches it.
    /// </summary>
    /// <remarks>
    /// Nothing is written here. The point is that the observer sees where the mark will fall while
    /// they are still holding the disc up against the horizon, and can let go if the sun is not
    /// where they wanted it — the one moment a permanent mark can still be called off.
    /// </remarks>
    private static void Preflight(ItemSlot slot, EntityAgent byEntity)
    {
        if (!LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            return;
        }

        if (FindCrossing(slot, byEntity) is not { } sighting)
        {
            return;
        }

        SkyDiscReadingState.ReportMark(
            $"The sun crosses at {SolarBandPolicy.Notch(sighting.Crossing.AzimuthDeg, SolarBandStore.NotchOf(slot.Itemstack)):0.#}° — hold the disc up to scratch it.");
    }

    /// <summary>
    /// Where the sun crossed the horizon today, or nothing — with the reason said out loud, once.
    /// </summary>
    private static SkyDiscSighting? FindCrossing(ItemSlot slot, EntityAgent byEntity)
    {
        var world = byEntity.World;
        if (world is null || slot?.Itemstack is null)
        {
            return null;
        }

        var isLocal = LocalObservationGate.IsLocalPlayerInteraction(byEntity);
        if (!SkyExposure.CanSeeSky(world.BlockAccessor, byEntity))
        {
            Report(isLocal, "No horizon from in here.");
            return null;
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
            return null;
        }

        var latitude = LatitudeMapper.MapGameLatitude(
            byEntity.Pos.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));

        return new SkyDiscSighting(crossing, latitude);
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
        if (slot?.Itemstack is not { } stack
            || FindCrossing(slot, byEntity) is not ({ } crossing, var latitude))
        {
            return;
        }

        var world = byEntity.World;
        var isLocal = LocalObservationGate.IsLocalPlayerInteraction(byEntity);
        var band = SolarBandStore.ReadOrEmpty(stack);
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

        SolarBandStore.Write(stack, band);

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
        byEntity?.Attributes?.RemoveAttribute(ScratchedAttribute);

        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            SkyDiscReadingState.End();
        }
    }
}

/// <summary>The sun at the horizon, and the latitude it was seen from.</summary>
internal sealed record SkyDiscSighting(SolarCrossing Crossing, double LatitudeDeg);
