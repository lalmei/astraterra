namespace AstraTerra.Constellations;

public sealed record ConstellationEdge(int A, int B, long EdgeOrder)
{
    public ConstellationEdge Normalize() => A <= B ? this : new ConstellationEdge(B, A, EdgeOrder);

    public bool Matches(int a, int b)
    {
        var min = Math.Min(a, b);
        var max = Math.Max(a, b);
        return A == min && B == max;
    }
}

public static class ConstellationGraph
{
    public static IReadOnlyList<IReadOnlyList<ConstellationEdge>> SplitConnectedComponents(IEnumerable<ConstellationEdge> edges)
    {
        var normalized = edges.Select(edge => edge.Normalize()).OrderBy(edge => edge.EdgeOrder).ToList();
        var adjacency = new Dictionary<int, List<ConstellationEdge>>();

        foreach (var edge in normalized)
        {
            adjacency.TryAdd(edge.A, []);
            adjacency.TryAdd(edge.B, []);
            adjacency[edge.A].Add(edge);
            adjacency[edge.B].Add(edge);
        }

        var visitedStars = new HashSet<int>();
        var components = new List<IReadOnlyList<ConstellationEdge>>();

        foreach (var start in adjacency.Keys.Order())
        {
            if (!visitedStars.Add(start))
            {
                continue;
            }

            var queue = new Queue<int>();
            var componentEdges = new HashSet<ConstellationEdge>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var star = queue.Dequeue();
                foreach (var edge in adjacency[star])
                {
                    componentEdges.Add(edge);
                    var other = edge.A == star ? edge.B : edge.A;
                    if (visitedStars.Add(other))
                    {
                        queue.Enqueue(other);
                    }
                }
            }

            if (componentEdges.Count > 0)
            {
                components.Add(componentEdges.OrderBy(edge => edge.EdgeOrder).ToList());
            }
        }

        return components;
    }
}
