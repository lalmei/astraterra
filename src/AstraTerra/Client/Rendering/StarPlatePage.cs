using AstraTerra.Astronomy;
using AstraTerra.Constellations;

namespace AstraTerra.Client.Rendering;

/// <summary>One plate in a book: a figure drawn out, with what the book knows about it underneath.</summary>
/// <param name="Sketch">
/// The figure fitted to the page, or empty when the catalog cannot place its stars — a page that
/// says a name and draws nothing is still the truth about what the book holds.
/// </param>
public sealed record StarPlatePage(
    string Title,
    SkyDiscFigureSketch Sketch,
    IReadOnlyList<string> Captions);

/// <summary>
/// Turns the figures a book holds into plates that can be looked at.
/// </summary>
/// <remarks>
/// The book's written page lists its constellations as counts — "stars=7; segments=6" — which is
/// what a text page can say about a shape. It is not what the observer drew. These are the same
/// records put back into the drawing they came from, using the projection the disc already engraves
/// with, so a figure on a page and the same figure on a disc are recognisably one figure.
/// <para>
/// Pure and catalog-fed, with no rendering in it, so what a plate says can be tested without a
/// screen to say it on.
/// </para>
/// </remarks>
public static class StarPlatePages
{
    /// <summary>How far a figure may reach from the middle of a plate, in the sketch's own units.</summary>
    /// <remarks>
    /// Larger than the disc's fit because a page has no rim to stay inside — only a margin, and the
    /// page is drawn to whatever this describes.
    /// </remarks>
    public const double FitRadius = 1.0;

    public static IReadOnlyList<StarPlatePage> Build(ConstellationJournal? journal, StarCatalog? catalog)
    {
        var records = journal?.Constellations ?? [];
        if (records.Count == 0)
        {
            return [];
        }

        var catalogByHip = catalog?.Stars.ToDictionary(star => star.Hip)
            ?? (IReadOnlyDictionary<int, StarCatalogEntry>)new Dictionary<int, StarCatalogEntry>();

        return records
            .OrderBy(record => record.Id)
            .Select(record => new StarPlatePage(
                ConstellationBookService.FormatDisplayName(record),
                SkyDiscFigureSketch.Project(record.Edges, catalogByHip, FitRadius),
                Captions(record)))
            .ToList();
    }

    private static IReadOnlyList<string> Captions(ConstellationRecord record)
    {
        var captions = new List<string>
        {
            $"{Count(ConstellationBookService.CountStars(record), "star")}, "
            + Count(record.Edges.Count, "line")
        };

        // Inherited figures, and figures drawn before the book kept who drew them, have nobody to
        // credit — exactly as on the written page, which says nothing rather than "unknown".
        if (!string.IsNullOrWhiteSpace(record.DiscoveredBy))
        {
            captions.Add($"drawn by {record.DiscoveredBy.Trim()}");
        }

        return captions;
    }

    private static string Count(int amount, string noun) => $"{amount} {noun}{(amount == 1 ? "" : "s")}";
}
