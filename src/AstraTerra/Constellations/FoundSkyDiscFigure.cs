using AstraTerra.Astronomy;

namespace AstraTerra.Constellations;

/// <summary>
/// The figure on a disc that somebody else engraved: one constellation, out of the sky they had.
/// </summary>
/// <remarks>
/// Taken from the shipped sky culture rather than drawn at random. A random scatter of lines between
/// random stars is not a constellation, it is noise, and the whole point of the object is that a
/// stranger looked at the same sky and thought this shape was worth carrying.
/// <para>
/// Two rules narrow the choice, and both come from the disc rather than from taste. A disc holds one
/// joined-up figure, so a culture's constellation that is drawn as several separate pieces
/// contributes its largest piece and not the rest. And the maker engraved what the maker could see:
/// a figure that never clears the horizon at the latitude their band was scribed at is not one they
/// could have drawn.
/// </para>
/// </remarks>
public static class FoundSkyDiscFigure
{
    /// <summary>How high a figure's stars must climb before somebody could be said to have seen it.</summary>
    private const double MinCulminationDeg = 15.0;

    /// <summary>The fewest lines that still make a shape rather than a stroke.</summary>
    private const int MinEdges = 3;

    public static SkyDiscFigure? Choose(StarCatalog? catalog, double latitudeDeg, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var candidates = Candidates(catalog, latitudeDeg);
        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    /// <summary>Every figure a maker at this latitude could have engraved, in catalogue order.</summary>
    public static IReadOnlyList<SkyDiscFigure> Candidates(StarCatalog? catalog, double latitudeDeg)
    {
        var constellations = catalog?.SkyCultures.FirstOrDefault()?.Constellations;
        if (constellations is null || constellations.Count == 0)
        {
            return [];
        }

        var declinations = catalog!.Stars.ToDictionary(star => star.Hip, star => star.DeclinationDeg);

        return constellations
            .Select(constellation => LargestPiece(constellation))
            .Where(edges => edges.Count >= MinEdges && EverSeenFrom(edges, declinations, latitudeDeg))
            .Select(edges => new SkyDiscFigure(edges.Select((edge, order) => new ConstellationEdge(edge.A, edge.B, order + 1))))
            .ToList();
    }

    /// <summary>
    /// The one joined-up piece of a constellation, which for most of them is all of it.
    /// </summary>
    private static IReadOnlyList<ConstellationEdge> LargestPiece(SkyCultureConstellation constellation)
    {
        var order = 0L;
        var edges = constellation.Lines
            .SelectMany(line => line.Zip(line.Skip(1), (a, b) => new ConstellationEdge(a, b, ++order)))
            .Where(edge => edge.A != edge.B)
            .ToList();

        return edges.Count == 0
            ? []
            : ConstellationGraph.SplitConnectedComponents(edges)
                .OrderByDescending(component => component.Count)
                .First();
    }

    /// <summary>
    /// Whether every star of a figure climbs high enough at this latitude to have been drawn from.
    /// </summary>
    /// <remarks>
    /// A star culminates at ninety degrees less the gap between the observer's latitude and its
    /// declination, which is all this needs: a figure with one limb that never rises is a figure
    /// nobody at that latitude engraved.
    /// </remarks>
    private static bool EverSeenFrom(
        IReadOnlyList<ConstellationEdge> edges,
        IReadOnlyDictionary<int, double> declinations,
        double latitudeDeg)
        => edges
            .SelectMany(edge => new[] { edge.A, edge.B })
            .Distinct()
            .All(star => declinations.TryGetValue(star, out var declination)
                         && 90.0 - Math.Abs(latitudeDeg - declination) >= MinCulminationDeg);
}
