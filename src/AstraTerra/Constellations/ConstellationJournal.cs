namespace AstraTerra.Constellations;

public sealed class ConstellationJournal
{
    private int nextId;
    private long nextTick;
    private long nextEdgeOrder;
    private readonly List<ConstellationRecord> constellations;

    public ConstellationJournal()
        : this(1, 1, 1, [])
    {
    }

    internal ConstellationJournal(int nextId, long nextTick, long nextEdgeOrder, IEnumerable<ConstellationRecord> constellations)
    {
        this.nextId = nextId;
        this.nextTick = nextTick;
        this.nextEdgeOrder = nextEdgeOrder;
        this.constellations = constellations.ToList();
    }

    public IReadOnlyList<ConstellationRecord> Constellations => constellations;

    public ConstellationRecord CreateFromEdge(int a, int b) => CreateFromEdges((a, b));

    public ConstellationRecord CreateFromEdges(params (int A, int B)[] edges)
    {
        var tick = nextTick++;
        var record = new ConstellationRecord(
            nextId++,
            null,
            tick,
            tick,
            edges.Select(CreateEdge).ToList());
        constellations.Add(record);
        return record;
    }

    public ConstellationRecord AddEdgeAndMerge(int a, int b)
    {
        var edge = CreateEdge((a, b));
        var touched = constellations
            .Where(record => record.Edges.Any(existing => existing.A == edge.A || existing.A == edge.B || existing.B == edge.A || existing.B == edge.B))
            .ToList();

        if (touched.Count == 0)
        {
            return CreateFromEdges((a, b));
        }

        foreach (var record in touched)
        {
            constellations.Remove(record);
        }

        var tick = nextTick++;
        var survivingName = touched.OrderByDescending(record => record.ModifiedTick).FirstOrDefault(record => !string.IsNullOrWhiteSpace(record.Name))?.Name;
        var mergedEdges = touched.SelectMany(record => record.Edges)
            .Append(edge)
            .GroupBy(existing => (existing.A, existing.B))
            .Select(group => group.OrderBy(existing => existing.EdgeOrder).First())
            .OrderBy(existing => existing.EdgeOrder)
            .ToList();

        var merged = new ConstellationRecord(touched.Min(record => record.Id), survivingName, touched.Min(record => record.CreatedTick), tick, mergedEdges);
        constellations.Add(merged);
        return merged;
    }

    public IReadOnlyList<ConstellationRecord> RemoveEdgeAndSplit(int id, int a, int b)
    {
        var record = constellations.Single(constellation => constellation.Id == id);
        constellations.Remove(record);

        var remainingEdges = record.Edges.Where(edge => !edge.Matches(a, b)).ToList();
        var components = ConstellationGraph.SplitConnectedComponents(remainingEdges);
        var splitRecords = new List<ConstellationRecord>();
        var largest = components.OrderByDescending(component => component.SelectMany(edge => new[] { edge.A, edge.B }).Distinct().Count())
            .ThenBy(component => component.Min(edge => edge.EdgeOrder))
            .FirstOrDefault();

        foreach (var component in components)
        {
            var isLargest = ReferenceEquals(component, largest);
            var split = new ConstellationRecord(
                isLargest ? record.Id : nextId++,
                isLargest ? record.Name : null,
                isLargest ? record.CreatedTick : nextTick,
                nextTick++,
                component.ToList());
            constellations.Add(split);
            splitRecords.Add(split);
        }

        return splitRecords;
    }

    public bool Remove(int id)
    {
        var record = constellations.FirstOrDefault(constellation => constellation.Id == id);
        return record is not null && constellations.Remove(record);
    }

    public void Replace(ConstellationRecord updated)
    {
        var index = constellations.FindIndex(record => record.Id == updated.Id);
        if (index < 0)
        {
            throw new InvalidOperationException($"Constellation #{updated.Id} does not exist.");
        }

        constellations[index] = updated with { ModifiedTick = nextTick++ };
    }

    internal JournalSnapshot ToSnapshot()
        => new(ConstellationPersistence.CurrentSchemaVersion, nextId, nextTick, nextEdgeOrder, constellations);

    internal static ConstellationJournal FromSnapshot(JournalSnapshot snapshot)
    {
        if (snapshot.Version != ConstellationPersistence.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported constellation journal version {snapshot.Version}.");
        }

        return new ConstellationJournal(snapshot.NextId, snapshot.NextTick, snapshot.NextEdgeOrder, snapshot.Constellations);
    }

    private ConstellationEdge CreateEdge((int A, int B) edge)
    {
        var min = Math.Min(edge.A, edge.B);
        var max = Math.Max(edge.A, edge.B);
        return new ConstellationEdge(min, max, nextEdgeOrder++);
    }
}

public sealed record JournalSnapshot(
    int Version,
    int NextId,
    long NextTick,
    long NextEdgeOrder,
    IReadOnlyList<ConstellationRecord> Constellations
);
