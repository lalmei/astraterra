using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class ConstellationGraphTests
{
    [Fact]
    public void Adding_Duplicate_Edge_Does_Not_Create_A_Second_Segment()
    {
        var journal = new ConstellationJournal();

        journal.AddEdgeAndMerge(100, 200);
        journal.AddEdgeAndMerge(200, 100);

        var constellation = Assert.Single(journal.Constellations);
        Assert.Single(constellation.Edges);
    }

    [Fact]
    public void Single_Segment_Constellation_Is_Valid()
    {
        var journal = new ConstellationJournal();

        var record = journal.CreateFromEdge(100, 200);

        Assert.Single(record.Edges);
        Assert.Equal(2, record.Edges.SelectMany(edge => new[] { edge.A, edge.B }).Distinct().Count());
    }

    [Fact]
    public void Connecting_From_Existing_Constellation_Extends_It()
    {
        var journal = new ConstellationJournal();
        var original = journal.CreateFromEdge(100, 200);

        var extended = journal.AddEdgeAndMerge(200, 300);

        Assert.Single(journal.Constellations);
        Assert.Equal(original.Id, extended.Id);
        Assert.Equal(2, extended.Edges.Count);
    }

    [Fact]
    public void Connecting_Two_Constellations_Merges_Them()
    {
        var journal = new ConstellationJournal();
        var first = journal.CreateFromEdge(100, 200);
        journal.CreateFromEdge(300, 400);

        var merged = journal.AddEdgeAndMerge(200, 300);

        Assert.Single(journal.Constellations);
        Assert.Equal(first.Id, merged.Id);
        Assert.Equal(3, merged.Edges.Count);
    }

    [Fact]
    public void Removing_A_Bridge_Splits_Into_Two_Components()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((1, 2), (2, 3), (3, 4));

        var split = journal.RemoveEdgeAndSplit(record.Id, 2, 3);

        Assert.Equal(2, split.Count);
    }

    [Fact]
    public void Removing_A_Bridge_Keeps_Name_On_Larger_Component()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (2, 10));
        journal.Replace(record with { Name = "Lantern" });

        var split = journal.RemoveEdgeAndSplit(record.Id, 2, 3);

        var named = Assert.Single(split, constellation => constellation.Name == "Lantern");
        Assert.Contains(named.Edges, edge => edge.Matches(3, 4));
        Assert.Contains(named.Edges, edge => edge.Matches(4, 5));
    }

    [Fact]
    public void Removing_A_Bridge_Uses_Oldest_Edge_As_Tie_Breaker()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((1, 2), (2, 3), (3, 4));
        journal.Replace(record with { Name = "Crossing" });

        var split = journal.RemoveEdgeAndSplit(record.Id, 2, 3);

        var named = Assert.Single(split, constellation => constellation.Name == "Crossing");
        Assert.Contains(named.Edges, edge => edge.Matches(1, 2));
    }
}
