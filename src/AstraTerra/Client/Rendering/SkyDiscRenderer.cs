using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// The disc's face, while its owner holds it up.
/// </summary>
/// <remarks>
/// Holder-only and text for now: your sky, in your hand. The face shows the two rims, where today's
/// mark fell against them, and — only once a rim has been seen to turn and come back — the year it
/// measured and how far from the equator it was scribed.
/// <para>
/// It never announces a solstice on the day one happens. At the time nobody can know an extreme was
/// one, and that honesty is the discovery: the player has a suspicion, which is the correct thing to
/// have until the sun comes back.
/// </para>
/// </remarks>
public sealed class SkyDiscRenderer : IRenderer
{
    private const float LineHeight = 26.0f;
    private const float TopOffset = 64.0f;


    private readonly ICoreClientAPI api;
    private readonly LoadedTexture?[] lines = new LoadedTexture?[5];
    private readonly string?[] lineTexts = new string?[5];

    public SkyDiscRenderer(ICoreClientAPI api)
    {
        this.api = api;
    }

    public double RenderOrder => 0.99;

    public int RenderRange => 9999;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || !SkyDiscReadingState.IsReading || !IsHoldingDisc())
        {
            return;
        }

        var slot = api.World.Player.InventoryManager.ActiveHotbarSlot;
        var band = SolarBandStore.ReadOrEmpty(slot?.Itemstack);
        var day = (int)Math.Floor(api.World.Calendar.TotalDays);

        RenderLine(0, slot?.Itemstack?.GetName() ?? "Sky disc");
        var sunset = band.Read(SolarEvent.Sunset);
        var sunrise = band.Read(SolarEvent.Sunrise);

        RenderLine(1, $"Sunsets: {sunset.Describe()}");
        RenderLine(2, $"Sunrises: {sunrise.Describe()}");

        if (SkyDiscReadingState.LastMarkMessage is { } marked)
        {
            RenderLine(3, marked);
            ClearLine(4);
            return;
        }

        var forecasts = new[]
        {
            DescribeNextTurn(sunset, band.BoundLatitudeDeg, day),
            DescribeNextTurn(sunrise, band.BoundLatitudeDeg, day)
        }.Where(forecast => forecast is not null).ToList();

        RenderLine(
            3,
            forecasts.FirstOrDefault()
            ?? "Sneak and hold right click as the sun touches the horizon.");
        RenderOptionalLine(4, forecasts.Skip(1).FirstOrDefault());
    }

    public void Dispose()
    {
        for (var i = 0; i < lines.Length; i++)
        {
            DeleteLine(i);
        }
    }

    /// <summary>
    /// The payout a farmer cares about, and the only forecast bronze can make: when the sun will next
    /// stand still. Offered only by a rim that has measured a whole year.
    /// </summary>
    private static string? DescribeNextTurn(SolarBandReading reading, double? latitudeDeg, int day)
    {
        if (reading.NextTurn(day) is not { } turn)
        {
            return null;
        }

        var subject = reading.Event == SolarEvent.Sunset ? "sunset" : "sunrise";
        var away = turn.Day - day;
        return away <= 0
            ? $"From the {subject} band alone, the {subject} is at its {turn.SolsticeName(latitudeDeg)} near {turn.NotchDeg:0.#}° about now."
            : $"From the {subject} band alone, the {subject} should reach its {turn.SolsticeName(latitudeDeg)} near {turn.NotchDeg:0.#}° in about {away} days.";
    }

    private bool IsHoldingDisc()
    {
        var code = api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code;
        return string.Equals(code?.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
            && code?.Path.StartsWith("sky-disc", StringComparison.OrdinalIgnoreCase) == true;
    }

    private void RenderLine(int index, string text)
    {
        if (lines[index] is null || lineTexts[index] != text)
        {
            DeleteLine(index);
            lineTexts[index] = text;
            lines[index] = api.Gui.TextTexture.GenTextTexture(
                text,
                new CairoFont(20, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
                null);
        }

        var texture = lines[index];
        if (texture is null || texture.TextureId == 0)
        {
            return;
        }

        var x = (api.Render.FrameWidth - texture.Width) / 2f;
        api.Render.Render2DLoadedTexture(texture, x, TopOffset + (index * LineHeight), 1001f);
    }

    private void RenderOptionalLine(int index, string? text)
    {
        if (text is null)
        {
            ClearLine(index);
            return;
        }

        RenderLine(index, text);
    }

    private void ClearLine(int index)
    {
        DeleteLine(index);
        lineTexts[index] = null;
    }

    private void DeleteLine(int index)
    {
        var texture = lines[index];
        if (texture is null)
        {
            return;
        }

        if (texture.TextureId != 0)
        {
            api.Render.GLDeleteTexture(texture.TextureId);
        }

        texture.TextureId = 0;
        lines[index] = null;
    }
}
