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
    private readonly LoadedTexture?[] lines = new LoadedTexture?[4];
    private readonly string?[] lineTexts = new string?[4];

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

        var band = SolarBandStore.ReadOrEmpty(api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack);
        var day = (int)Math.Floor(api.World.Calendar.TotalDays);

        RenderLine(0, "Bronze sky disc");
        RenderLine(1, $"Sunsets: {band.Read(SolarEvent.Sunset).Describe()}");
        RenderLine(2, $"Sunrises: {band.Read(SolarEvent.Sunrise).Describe()}");
        RenderLine(3, DescribeNextTurn(band, day) ?? "Sneak and right click as the sun touches the horizon.");
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
    private static string? DescribeNextTurn(SolarBand band, int day)
    {
        if (SkyDiscReadingState.LastMarkMessage is { } marked)
        {
            return marked;
        }

        foreach (var solarEvent in new[] { SolarEvent.Sunset, SolarEvent.Sunrise })
        {
            var reading = band.Read(solarEvent);
            if (reading.NextTurnDay(day) is { } turn)
            {
                var away = turn - day;
                return away <= 0
                    ? "The sun stands still about now."
                    : $"The sun should stand still again on day {turn}, {away} days from now.";
            }
        }

        return null;
    }

    private bool IsHoldingDisc()
    {
        var code = api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible?.Code;
        return string.Equals(code?.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
            && string.Equals(code?.Path, "sky-disc", StringComparison.OrdinalIgnoreCase);
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
