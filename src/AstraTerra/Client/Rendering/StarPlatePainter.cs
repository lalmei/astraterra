using Cairo;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Paints one plate onto a surface: the leaf, the title, the figure, and what the book says about it.
/// </summary>
/// <remarks>
/// Separate from the renderer that shows it, and free of the game's API, so a plate is a drawing that
/// can be produced and looked at anywhere — including outside a running client, which is how its
/// layout was checked without launching the game to squint at it.
/// </remarks>
public static class StarPlatePainter
{
    /// <summary>A page is taller than it is wide, the proportions of the book it is bound in.</summary>
    public const double WidthOverHeight = 0.74;

    /// <summary>What a plate says when the book it came from holds no figures at all.</summary>
    public static readonly IReadOnlyList<string> EmptyBookLines =
    [
        "This book holds no figures yet.",
        "Draw one through a telescope and it is bound in here."
    ];

    /// <summary>How much of a plate's width text may use, inside the ruled frame.</summary>
    private const double InnerWidthFraction = 0.84;

    /// <summary>What a plate says when this install cannot place the stars the figure names.</summary>
    public const string UnplaceableFigureLine = "The stars of this figure are not in the catalog.";

    public static void Paint(
        Context context,
        StarPlatePage? page,
        int width,
        int height,
        string footer,
        string fontName)
    {
        ArgumentNullException.ThrowIfNull(context);

        PaintLeaf(context, width, height);

        var margin = height * 0.07;
        if (page is null)
        {
            PaintCentredLines(context, width, height, height * 0.030, EmptyBookLines, fontName);
        }
        else
        {
            PaintTitle(context, page.Title, width, margin + (height * 0.045), fontName);
            PaintFigure(context, page, width, height, fontName);
            PaintCaptions(context, page.Captions, width, height - margin - (height * 0.055), fontName);
        }

        // Inside the ruled frame, not on it: a page number that touches the border reads as a
        // printing mistake rather than as a page number.
        PaintFooter(context, footer, width, height * 0.938, fontName);
    }

    /// <summary>The page itself: laid paper with a ruled border, and no shine on it.</summary>
    private static void PaintLeaf(Context context, int width, int height)
    {
        var radius = height * 0.02;

        context.Operator = Operator.Source;
        context.SetSourceRGBA(0, 0, 0, 0);
        context.Paint();
        context.Operator = Operator.Over;

        RoundedRectangle(context, 0, 0, width, height, radius);
        context.SetSourceRGBA(0.90, 0.86, 0.75, 0.97);
        context.FillPreserve();
        context.SetSourceRGBA(0.24, 0.18, 0.11, 0.85);
        context.LineWidth = Math.Max(1.5, height * 0.004);
        context.Stroke();

        // The ruled frame a plate is drawn inside, well in from the cut edge of the leaf.
        var inset = height * 0.035;
        RoundedRectangle(context, inset, inset, width - (inset * 2), height - (inset * 2), radius * 0.6);
        context.SetSourceRGBA(0.35, 0.26, 0.16, 0.45);
        context.LineWidth = Math.Max(1.0, height * 0.0018);
        context.Stroke();
    }

    private static void PaintTitle(Context context, string title, int width, double baseline, string fontName)
    {
        var size = width * 0.062;
        SelectFont(context, size, FontWeight.Bold, fontName);
        while (context.TextExtents(title).Width > width * InnerWidthFraction && size > width * 0.026)
        {
            size *= 0.94;
            SelectFont(context, size, FontWeight.Bold, fontName);
        }

        DrawCentred(context, title, width / 2.0, baseline, 0.16, 0.11, 0.06);

        var extents = context.TextExtents(title);
        var underlineHalf = Math.Min(width * 0.36, (extents.Width / 2.0) + (width * 0.03));
        context.MoveTo((width / 2.0) - underlineHalf, baseline + (size * 0.42));
        context.LineTo((width / 2.0) + underlineHalf, baseline + (size * 0.42));
        context.SetSourceRGBA(0.35, 0.26, 0.16, 0.55);
        context.LineWidth = Math.Max(1.0, width * 0.0022);
        context.Stroke();
    }

    /// <summary>
    /// The figure, inked the way it was drawn: lines first, then a star punched at every joint.
    /// </summary>
    /// <remarks>
    /// Stars go on top of the lines so a joint reads as one star with lines running into it, rather
    /// than as two strokes crossing near a dot. The sketch is fitted to a square, so the drawing
    /// keeps the proportions the sky gave it whatever shape the page is.
    /// </remarks>
    private static void PaintFigure(Context context, StarPlatePage page, int width, int height, string fontName)
    {
        var centreX = width / 2.0;
        var centreY = height * 0.46;
        var span = Math.Min(width * 0.80, height * 0.58) / 2.0;

        if (page.Sketch.Lines.Count == 0)
        {
            FitToWidth(context, [UnplaceableFigureLine], width * 0.035, width * InnerWidthFraction, fontName);
            DrawCentred(context, UnplaceableFigureLine, centreX, centreY, 0.35, 0.26, 0.16);
            return;
        }

        context.LineCap = LineCap.Round;
        context.LineWidth = Math.Max(1.6, span * 0.016);
        context.SetSourceRGBA(0.18, 0.16, 0.28, 0.92);
        foreach (var line in page.Sketch.Lines)
        {
            context.MoveTo(centreX + (line.StartX * span), centreY - (line.StartY * span));
            context.LineTo(centreX + (line.EndX * span), centreY - (line.EndY * span));
        }

        context.Stroke();

        var starRadius = Math.Max(2.0, span * 0.030);
        foreach (var star in page.Sketch.Stars)
        {
            var x = centreX + (star.X * span);
            var y = centreY - (star.Y * span);

            context.Arc(x, y, starRadius * 1.9, 0, Math.PI * 2);
            context.SetSourceRGBA(0.24, 0.20, 0.34, 0.18);
            context.Fill();

            context.Arc(x, y, starRadius, 0, Math.PI * 2);
            context.SetSourceRGBA(0.13, 0.11, 0.22, 0.95);
            context.Fill();
        }
    }

    private static void PaintCaptions(
        Context context,
        IReadOnlyList<string> captions,
        int width,
        double bottom,
        string fontName)
    {
        var size = FitToWidth(context, captions, width * 0.036, width * InnerWidthFraction, fontName);
        var baseline = bottom - ((captions.Count - 1) * size * 1.45);
        foreach (var caption in captions)
        {
            DrawCentred(context, caption, width / 2.0, baseline, 0.30, 0.22, 0.14);
            baseline += size * 1.45;
        }
    }

    private static void PaintFooter(Context context, string footer, int width, double baseline, string fontName)
    {
        // A book with no plates in it has no page to number, and an invented one would be a lie.
        if (string.IsNullOrWhiteSpace(footer))
        {
            return;
        }

        SelectFont(context, width * 0.030, FontWeight.Normal, fontName);
        DrawCentred(context, footer, width / 2.0, baseline, 0.40, 0.32, 0.22);
    }

    private static void DrawCentred(
        Context context,
        string text,
        double centreX,
        double baseline,
        double red,
        double green,
        double blue)
    {
        var extents = context.TextExtents(text);
        context.SetSourceRGB(red, green, blue);
        context.MoveTo(centreX - (extents.Width / 2.0) - extents.XBearing, baseline);
        context.ShowText(text);
        context.NewPath();
    }

    private static void PaintCentredLines(
        Context context,
        int width,
        int height,
        double size,
        IReadOnlyList<string> lines,
        string fontName)
    {
        var fitted = FitToWidth(context, lines, size, width * InnerWidthFraction, fontName);
        var baseline = (height / 2.0) - ((lines.Count - 1) * fitted * 0.8);
        foreach (var line in lines)
        {
            DrawCentred(context, line, width / 2.0, baseline, 0.30, 0.22, 0.14);
            baseline += fitted * 1.6;
        }
    }

    /// <summary>
    /// Shrinks a line of type until it fits between the ruled edges, and leaves the context using it.
    /// </summary>
    /// <remarks>
    /// A plate is drawn at whatever size the screen is, and the text on it is written in whatever
    /// language the client is set to. Neither is known when the sizes above are chosen, so a line
    /// long enough to run off the leaf is a normal thing rather than an error, and it is set smaller
    /// rather than allowed to overhang.
    /// </remarks>
    private static double FitToWidth(
        Context context,
        IReadOnlyList<string> lines,
        double startSize,
        double available,
        string fontName)
    {
        var size = startSize;
        for (var attempt = 0; attempt < 24; attempt++)
        {
            SelectFont(context, size, FontWeight.Normal, fontName);
            if (lines.Max(line => context.TextExtents(line).Width) <= available || size <= startSize * 0.35)
            {
                return size;
            }

            size *= 0.94;
        }

        return size;
    }

    private static void SelectFont(Context context, double size, FontWeight weight, string fontName)
    {
        context.SelectFontFace(fontName, FontSlant.Normal, weight);
        context.SetFontSize(size);
    }

    private static void RoundedRectangle(
        Context context,
        double x,
        double y,
        double width,
        double height,
        double radius)
    {
        var limited = Math.Min(radius, Math.Min(width, height) / 2.0);
        context.NewPath();
        context.Arc(x + width - limited, y + limited, limited, -Math.PI / 2, 0);
        context.Arc(x + width - limited, y + height - limited, limited, 0, Math.PI / 2);
        context.Arc(x + limited, y + height - limited, limited, Math.PI / 2, Math.PI);
        context.Arc(x + limited, y + limited, limited, Math.PI, Math.PI * 1.5);
        context.ClosePath();
    }
}
