using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// The plates in a held journal: its constellations drawn out, one to a page.
/// </summary>
/// <remarks>
/// Vintage Story renders a book from its <c>text</c> attribute, which is why the journal has always
/// read as a list of counts. A drawn figure cannot be said in text, so the plate is drawn over the
/// view instead — the same bargain the disc's face makes, and for the same reason: what the
/// instrument says is a shape, and a shape has to be looked at.
/// <para>
/// Each plate is painted once and kept as a texture until the page or the window changes. Figures
/// are static drawings; repainting one sixty times a second would buy nothing. The painting itself
/// is <see cref="StarPlatePainter"/>'s and knows nothing of the game.
/// </para>
/// </remarks>
public sealed class StarPlateRenderer : IRenderer
{
    /// <summary>How much of the screen's height a plate takes.</summary>
    private const double HeightFraction = 0.74;

    private readonly ICoreClientAPI api;
    private readonly ConstellationBookClient bookClient;
    private readonly StarCatalog? catalog;

    private int textureId;
    private string? paintedKey;

    public StarPlateRenderer(ICoreClientAPI api, ConstellationBookClient bookClient, StarCatalog? catalog)
    {
        this.api = api;
        this.bookClient = bookClient;
        this.catalog = catalog;
    }

    public double RenderOrder => 0.98;

    public int RenderRange => 9999;

    /// <summary>How many plates the book in hand holds right now.</summary>
    /// <remarks>
    /// Read from the book each time rather than kept, so swapping books turns the reader straight to
    /// the new one's plates. The wheel asks this before it turns a page.
    /// </remarks>
    public int CurrentPageCount() => BuildPages().Count;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || !StarPlateState.IsOpen)
        {
            return;
        }

        // A book put away shuts itself. Leaving the plate hanging over the view of someone who is no
        // longer holding it would be a window, not a page.
        if (!bookClient.HasHeldJournalBook())
        {
            StarPlateState.Close();
            Release();
            return;
        }

        var pages = BuildPages();
        var height = (int)Math.Round(api.Render.FrameHeight * HeightFraction);
        var width = (int)Math.Round(height * StarPlatePainter.WidthOverHeight);
        if (width < 16 || height < 16)
        {
            return;
        }

        var index = StarPlateState.ClampPage(pages.Count);
        var page = pages.Count == 0 ? null : pages[index];
        var footer = Footer(index, pages.Count);
        var key = $"{width}x{height}|{footer}|{Describe(page)}";
        if (key != paintedKey)
        {
            Repaint(page, width, height, footer);
            paintedKey = textureId == 0 ? null : key;
        }

        if (textureId == 0)
        {
            return;
        }

        api.Render.Render2DTexture(
            textureId,
            (api.Render.FrameWidth - width) / 2f,
            (api.Render.FrameHeight - height) / 2f,
            width,
            height,
            1000f);
    }

    public void Dispose() => Release();

    private IReadOnlyList<StarPlatePage> BuildPages()
        => StarPlatePages.Build(bookClient.ReadCurrentJournal(), catalog);

    private static string Footer(int index, int pageCount)
        => pageCount == 0
            ? string.Empty
            : pageCount == 1
            ? Lang.Get("astraterra:starplate-footer-single")
            : Lang.Get("astraterra:starplate-footer", index + 1, pageCount);

    /// <summary>Everything about a plate that changes what it looks like, and nothing that does not.</summary>
    private static string Describe(StarPlatePage? page)
        => page is null
            ? "blank"
            : $"{page.Title}|{page.Sketch.Lines.Count}|{page.Sketch.Stars.Count}|"
              + string.Join(";", page.Captions);

    private void Repaint(StarPlatePage? page, int width, int height, string footer)
    {
        Release();

        using var surface = new ImageSurface(Format.Argb32, width, height);
        using var context = new Context(surface);

        StarPlatePainter.Paint(context, page, width, height, footer, GuiStyle.StandardFontName);

        surface.Flush();
        textureId = api.Gui.LoadCairoTexture(surface, true);
    }

    private void Release()
    {
        if (textureId != 0)
        {
            api.Render.GLDeleteTexture(textureId);
            textureId = 0;
        }

        paintedKey = null;
    }
}
