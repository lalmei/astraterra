using AstraTerra.Astronomy;

namespace AstraTerra.Constellations;

public static class StarCatalogJournalBuilder
{
    public const string ModernIauCultureId = "modern_iau";

    public static ConstellationJournal Build(StarCatalog catalog, string cultureId = ModernIauCultureId)
    {
        var culture = catalog.SkyCultures.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, cultureId, StringComparison.OrdinalIgnoreCase));
        if (culture is null)
        {
            throw new InvalidOperationException($"Sky culture not found: {cultureId}");
        }

        var availableStars = catalog.Stars.Select(star => star.Hip).ToHashSet();
        var journal = new ConstellationJournal();

        foreach (var constellation in culture.Constellations)
        {
            var edges = FlattenLines(constellation.Lines)
                .Where(edge => availableStars.Contains(edge.A) && availableStars.Contains(edge.B))
                .Distinct()
                .ToArray();
            if (edges.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Authored constellation {constellation.DisplayName} ({constellation.IauCode}) has no available line stars.");
            }

            var record = journal.CreateFromEdges(edges);
            journal.Replace(record with { Name = constellation.DisplayName });
        }

        return journal;
    }

    private static IEnumerable<(int A, int B)> FlattenLines(IReadOnlyList<IReadOnlyList<int>> lines)
    {
        foreach (var line in lines)
        {
            for (var index = 1; index < line.Count; index++)
            {
                var first = Math.Min(line[index - 1], line[index]);
                var second = Math.Max(line[index - 1], line[index]);
                if (first != second)
                {
                    yield return (first, second);
                }
            }
        }
    }
}
