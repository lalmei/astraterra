using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Items;

/// <summary>
/// Right click reads the sky. Sneak and right click cuts a new plate for wherever the instrument is
/// standing — see <see cref="AstrolabeCalibrationPolicy"/> for why the plate has to be cut at all.
/// </summary>
public sealed class ItemAstrolabe : Item
{
    /// <summary>
    /// Which of the two interactions is running, decided once at the start.
    /// </summary>
    /// <remarks>
    /// Read from the entity rather than kept in a field, because an <see cref="Item"/> is one shared
    /// instance for the whole item type: a field here would be every astrolabe in the world at once.
    /// It also has to be remembered rather than re-read from the sneak key each step — letting go of
    /// sneak halfway through would otherwise turn a part-finished sighting into a reading, and the
    /// seconds already spent would silently carry over into whatever came next.
    /// </remarks>
    private const string CalibratingAttribute = "astraterraAstrolabeCalibrating";

    /// <summary>
    /// The <c>secondsUsed</c> the current uninterrupted sighting began at. Walking under a roof
    /// mid-sighting restarts the count here, so the progress the HUD shows is progress that
    /// actually happened.
    /// </summary>
    private const string SightingFromAttribute = "astraterraAstrolabeSightingFrom";

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;

        var calibrating = byEntity?.Controls?.Sneak == true;
        byEntity?.Attributes?.SetBool(CalibratingAttribute, calibrating);
        byEntity?.Attributes?.SetFloat(SightingFromAttribute, 0f);

        if (calibrating)
        {
            StepCalibration(slot, byEntity!, secondsUsed: 0f);
            return;
        }

        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            AstrolabeReadingState.Begin();
        }
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        if (byEntity is not null && byEntity.Attributes?.GetBool(CalibratingAttribute) == true)
        {
            return StepCalibration(slot, byEntity, secondsUsed);
        }

        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            AstrolabeReadingState.Begin();
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
        Finish(byEntity);
    }

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
    /// One frame of the sighting. Returns whether the interaction should keep running.
    /// </summary>
    /// <remarks>
    /// A blocked sighting keeps running rather than cancelling: the player is holding the button
    /// with the instrument raised, and stepping out from a doorway should finish the job rather than
    /// require a second attempt. The HUD says what is in the way meanwhile.
    /// </remarks>
    private static bool StepCalibration(ItemSlot slot, EntityAgent byEntity, float secondsUsed)
    {
        var world = byEntity.World;
        if (world is null)
        {
            return false;
        }

        var canSeeSky = SkyExposure.CanSeeSky(world.BlockAccessor, byEntity);
        // DayLightStrength is client-only; the same 0-to-1 term is available on both sides here, and
        // the server has to reach it because the server is what decides whether the plate is cut.
        var daylight = world.Calendar.GetDayLightStrength(byEntity.Pos.X, byEntity.Pos.Z);
        var obstacle = AstrolabeCalibrationPolicy.DescribeObstacle(canSeeSky, daylight);
        var isLocal = LocalObservationGate.IsLocalPlayerInteraction(byEntity);

        if (obstacle is not null)
        {
            byEntity.Attributes?.SetFloat(SightingFromAttribute, secondsUsed);
            if (isLocal)
            {
                AstrolabeCalibrationState.Update(secondsUsed, obstacle);
            }
            return true;
        }

        var elapsed = Math.Max(0f, secondsUsed - (byEntity.Attributes?.GetFloat(SightingFromAttribute, 0f) ?? 0f));
        if (isLocal)
        {
            AstrolabeCalibrationState.Update(elapsed, obstacle: null);
        }

        if (!AstrolabeCalibrationPolicy.IsComplete(elapsed))
        {
            return true;
        }

        // The server owns the itemstack, so it owns the plate. The client stops here too, which ends
        // both sides on the same frame and takes the progress bar down when the bar reads full.
        if (world.Side == EnumAppSide.Server)
        {
            CutPlate(slot, byEntity, world);
        }

        return false;
    }

    private static void CutPlate(ItemSlot slot, EntityAgent byEntity, IWorldAccessor world)
    {
        if (slot?.Itemstack is null)
        {
            return;
        }

        var calendar = world.Calendar;
        var latitude = LatitudeMapper.MapGameLatitude(
            byEntity.Pos.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));

        AstrolabeCalibrationStore.Write(slot.Itemstack, latitude, calendar.TotalDays);

        // Without this the plate exists only in the server's copy of the stack, and the HUD that
        // reads it is on the client.
        slot.MarkDirty();
    }

    private static void Finish(EntityAgent? byEntity)
    {
        byEntity?.Attributes?.RemoveAttribute(CalibratingAttribute);
        byEntity?.Attributes?.RemoveAttribute(SightingFromAttribute);

        if (LocalObservationGate.IsLocalPlayerInteraction(byEntity))
        {
            AstrolabeReadingState.End();
            AstrolabeCalibrationState.End();
        }
    }
}
