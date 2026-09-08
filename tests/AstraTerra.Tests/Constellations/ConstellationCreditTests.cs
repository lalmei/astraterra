using AstraTerra.Constellations;
using Vintagestory.API.Datastructures;
using Xunit;

namespace AstraTerra.Tests.Constellations;

/// <summary>
/// Who drew a figure, which the book keeps so a journal that changes hands still says whose work it
/// was.
/// </summary>
public sealed class ConstellationCreditTests
{
    [Fact]
    public void Drawing_A_New_Figure_Writes_Down_Who_Drew_It()
    {
        var journal = new ConstellationJournal();

        var record = journal.AddEdgeAndMerge(100, 200, "Astra");

        Assert.Equal("Astra", record.DiscoveredBy);
    }

    [Fact]
    public void Joining_Two_Figures_Leaves_The_Credit_With_Whoever_Drew_The_Older_One()
    {
        var journal = new ConstellationJournal();
        journal.AddEdgeAndMerge(100, 200, "Astra");
        journal.AddEdgeAndMerge(300, 400, "Borrower");

        // The connecting segment merges both figures into one.
        var merged = journal.AddEdgeAndMerge(200, 300, "Borrower");

        Assert.Equal("Astra", merged.DiscoveredBy);
    }

    [Fact]
    public void Splitting_A_Figure_Leaves_Every_Part_Credited_To_Its_Drawer()
    {
        var journal = new ConstellationJournal();
        journal.AddEdgeAndMerge(100, 200, "Astra");
        journal.AddEdgeAndMerge(200, 300, "Astra");
        var figure = journal.AddEdgeAndMerge(300, 400, "Astra");

        var parts = journal.RemoveEdgeAndSplit(figure.Id, 200, 300);

        Assert.Equal(2, parts.Count);
        Assert.All(parts, part => Assert.Equal("Astra", part.DiscoveredBy));
    }

    [Fact]
    public void An_Unattributed_Figure_Stays_Unattributed()
    {
        var journal = new ConstellationJournal();

        var record = journal.AddEdgeAndMerge(100, 200);

        Assert.Null(record.DiscoveredBy);
    }

    [Fact]
    public void A_Blank_Drawer_Is_Kept_As_Nobody_Rather_Than_Whitespace()
    {
        var journal = new ConstellationJournal();

        var record = journal.AddEdgeAndMerge(100, 200, "   ");

        Assert.Null(record.DiscoveredBy);
    }

    [Fact]
    public void The_Drawer_Survives_A_Round_Trip()
    {
        var journal = new ConstellationJournal();
        journal.AddEdgeAndMerge(100, 200, "Astra");

        var restored = ConstellationPersistence.Deserialize(ConstellationPersistence.Serialize(journal));

        Assert.Equal("Astra", restored.Constellations.Single().DiscoveredBy);
    }

    [Fact]
    public void A_Journal_Written_Before_Drawers_Were_Recorded_Still_Reads()
    {
        var restored = ConstellationPersistence.Deserialize(
            """
            {
              "version": 1,
              "nextId": 2,
              "nextTick": 2,
              "nextEdgeOrder": 2,
              "constellations": [
                {
                  "id": 1,
                  "name": "Lantern",
                  "createdTick": 1,
                  "modifiedTick": 1,
                  "edges": [ { "a": 100, "b": 200, "edgeOrder": 1 } ]
                }
              ]
            }
            """);

        var record = restored.Constellations.Single();
        Assert.Equal("Lantern", record.Name);
        Assert.Null(record.DiscoveredBy);
    }

    [Fact]
    public void The_Page_Credits_Whoever_Drew_A_Figure()
    {
        var journal = new ConstellationJournal();
        journal.Replace(journal.AddEdgeAndMerge(100, 200, "Astra") with { Name = "Lantern" });
        journal.Replace(journal.AddEdgeAndMerge(300, 400) with { Name = "Inherited" });
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteJournal(attributes, journal);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);
        Assert.Contains("drawn by Astra", text);
        Assert.DoesNotContain("Inherited: stars=2; segments=1; drawn by", text);
    }
}
