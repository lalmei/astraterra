using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class PlanetJournalTests
{
    [Fact]
    public void An_Unrecorded_Planet_Is_Just_A_Wandering_Star()
    {
        var journal = new PlanetJournal();

        Assert.False(journal.IsIdentified("mars"));
        Assert.Equal(PlanetJournal.UnidentifiedDisplayName, journal.DisplayName("mars"));
    }

    [Fact]
    public void Identifying_A_Planet_Does_Not_Name_It()
    {
        var journal = new PlanetJournal();

        var record = journal.Identify("mars");

        Assert.True(journal.IsIdentified("mars"));
        Assert.Null(record.Name);
        Assert.Contains(PlanetJournal.UnidentifiedDisplayName, journal.DisplayName("mars"));
        Assert.Contains("unnamed", journal.DisplayName("mars"));
    }

    [Fact]
    public void Identifying_The_Same_Planet_Twice_Records_It_Once()
    {
        var journal = new PlanetJournal();

        journal.Identify("mars");
        journal.Identify("mars");

        Assert.Single(journal.Planets);
    }

    [Fact]
    public void Naming_A_Planet_Keeps_The_Observers_Own_Word_For_It()
    {
        var journal = new PlanetJournal();

        journal.Rename("mars", "  The Red Wanderer  ");

        Assert.Equal("The Red Wanderer", journal.DisplayName("mars"));
    }

    [Fact]
    public void Naming_An_Unseen_Planet_Records_It_Too()
    {
        var journal = new PlanetJournal();

        journal.Rename("venus", "Evening Lamp");

        Assert.True(journal.IsIdentified("venus"));
        Assert.Equal("Evening Lamp", journal.DisplayName("venus"));
    }

    [Fact]
    public void Clearing_A_Name_Leaves_The_Planet_Identified()
    {
        var journal = new PlanetJournal();
        journal.Rename("venus", "Evening Lamp");

        journal.Rename("venus", "   ");

        Assert.True(journal.IsIdentified("venus"));
        Assert.Contains("unnamed", journal.DisplayName("venus"));
    }

    [Fact]
    public void A_Removed_Planet_Goes_Back_To_Being_Anonymous()
    {
        var journal = new PlanetJournal();
        journal.Rename("saturn", "Slow One");

        Assert.True(journal.Remove("saturn"));
        Assert.False(journal.Remove("saturn"));
        Assert.Equal(PlanetJournal.UnidentifiedDisplayName, journal.DisplayName("saturn"));
    }

    [Fact]
    public void A_Journal_Survives_A_Round_Trip_Through_A_Book()
    {
        var journal = new PlanetJournal();
        journal.Rename("mars", "The Red Wanderer");
        journal.Identify("jupiter");

        var restored = PlanetJournalPersistence.Deserialize(PlanetJournalPersistence.Serialize(journal));

        Assert.Equal(2, restored.Planets.Count);
        Assert.Equal("The Red Wanderer", restored.DisplayName("mars"));
        Assert.True(restored.IsIdentified("jupiter"));
        Assert.Null(restored.Find("jupiter")!.Name);
    }

    [Fact]
    public void A_Restored_Journal_Keeps_Recording_Without_Reusing_A_Tick()
    {
        var journal = new PlanetJournal();
        journal.Identify("mars");
        journal.Identify("venus");

        var restored = PlanetJournalPersistence.Deserialize(PlanetJournalPersistence.Serialize(journal));
        var next = restored.Identify("saturn");

        Assert.True(next.RecordedTick > restored.Find("venus")!.RecordedTick);
    }

    [Fact]
    public void Junk_Entries_Are_Dropped_Rather_Than_Breaking_The_Book()
    {
        var restored = PlanetJournalPersistence.Deserialize(
            """
            {
              "schemaVersion": 1,
              "nextTick": 4,
              "planets": [
                { "planetId": "mars", "name": "Red", "recordedTick": 1 },
                { "planetId": "  ", "name": "Nothing", "recordedTick": 2 }
              ]
            }
            """);

        Assert.Single(restored.Planets);
        Assert.Equal("Red", restored.DisplayName("mars"));
    }

    [Fact]
    public void An_Empty_Planet_Id_Is_Rejected()
    {
        var journal = new PlanetJournal();

        Assert.Throws<ArgumentException>(() => journal.Identify("  "));
    }

    [Theory]
    [InlineData(ConstellationBookMutationActions.RenamePlanet, true)]
    [InlineData(ConstellationBookMutationActions.AddEdge, false)]
    [InlineData(ConstellationBookMutationActions.Rename, false)]
    [InlineData(ConstellationBookMutationActions.Delete, false)]
    [InlineData(ConstellationBookMutationActions.Build, false)]
    [InlineData(ConstellationBookMutationActions.ClassifySighting, false)]
    [InlineData("identifyPlanet", false)]
    [InlineData("nonsense", false)]
    public void Only_Planet_Actions_Write_The_Planet_Half_Of_A_Book(string action, bool expected)
    {
        // The server routes on this: a constellation action must never land in the planet journal,
        // and a planet action must never rewrite the drawn figures.
        Assert.Equal(expected, ConstellationBookMutationActions.IsPlanetAction(action));
    }
}
