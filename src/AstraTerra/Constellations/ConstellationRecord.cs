namespace AstraTerra.Constellations;

/// <param name="DiscoveredBy">
/// Who drew the figure, kept so a book that changes hands still says whose work it was. Null for a
/// prepared book, whose figures were inherited rather than invented, and for every figure drawn
/// before the book started recording it.
/// </param>
public sealed record ConstellationRecord(
    int Id,
    string? Name,
    long CreatedTick,
    long ModifiedTick,
    IReadOnlyList<ConstellationEdge> Edges,
    string? DiscoveredBy = null
);
