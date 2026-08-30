using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// The disc itself, held up into the lower half of the view while its owner reads or marks it.
/// </summary>
/// <remarks>
/// A scratch is the whole of what this instrument says, and at arm's length it is a few pixels. So
/// the disc comes up big, drawn from the stack in hand — what is on the screen is this disc, with
/// this disc's marks, rather than a picture of one. The wheel turns it, the way a disc in the hand
/// is turned to bring a mark round to the eye.
/// <para>
/// It is a HUD rather than a plain overlay renderer because drawing an itemstack goes through the
/// GUI shader, and that shader is only in use while dialogs are being drawn. The same call from a
/// renderer registered on the ortho stage sets its uniforms on a program nobody is running, and
/// draws nothing at all.
/// </para>
/// <remarks>
/// It lives with the disc's other drawing rather than with the mod's dialogs: it asks nothing of the
/// player and holds no controls — it is the instrument being drawn, in a dialog only because that is
/// where the shader that can draw it is running.
/// </remarks>
public sealed class SkyDiscFaceHud : HudElement
{
    /// <summary>
    /// How much of the screen's height the raised disc takes, and how low it sits. The rim is where
    /// the marks are, so the disc is drawn as large as the view will hold it: at this fraction the
    /// whole of it still clears the top of the screen and the bottom, and nothing worth reading
    /// falls past an edge.
    /// </summary>
    private const float HeightFraction = 0.68f;

    private const float SinkFraction = 0.55f;

    public SkyDiscFaceHud(ICoreClientAPI capi)
        : base(capi)
    {
    }

    public override string ToggleKeyCombinationCode => null!;

    public override bool Focusable => false;

    public override bool ShouldReceiveRenderEvents() => true;

    public override void OnRenderGUI(float deltaTime)
    {
        base.OnRenderGUI(deltaTime);

        if (!SkyDiscReadingState.IsReading)
        {
            return;
        }

        var slot = capi.World.Player?.InventoryManager?.ActiveHotbarSlot;
        if (slot?.Itemstack?.Collectible is not Items.ItemSkyDisc)
        {
            return;
        }

        var size = capi.Render.FrameHeight * HeightFraction;

        // The disc's turn belongs to this one drawing of it. The item, and the GUI transform the
        // turn is applied to, are shared by every disc on the screen — the hotbar icon included —
        // so the item is told that the raised face is what it is being asked for, and untold again
        // the moment the call returns.
        SkyDiscReadingState.BeginDrawingRaisedFace();
        try
        {
            capi.Render.RenderItemstackToGui(
                slot,
                capi.Render.FrameWidth / 2.0,
                capi.Render.FrameHeight - (size * SinkFraction),
                100.0,
                (float)size,
                ColorUtil.WhiteArgb,
                deltaTime,
                true,
                false,
                false);
        }
        finally
        {
            SkyDiscReadingState.EndDrawingRaisedFace();
        }
    }
}
