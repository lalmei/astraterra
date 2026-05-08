namespace AstraTerra.Constellations;

public sealed record ConstellationRecord(
    int Id,
    string? Name,
    long CreatedTick,
    long ModifiedTick,
    IReadOnlyList<ConstellationEdge> Edges
);
