using AstraTerra.Commands;
using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Commands;

public sealed class StarsCommandServiceTests
{
    [Fact]
    public void List_Returns_Saved_Constellations()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(100, 200);
        journal.Replace(record with { Name = "Test Pattern" });

        var output = new StarsCommandService(journal).List();

        Assert.Contains("Test Pattern", output);
        Assert.Contains($"#{record.Id}", output);
    }

    [Fact]
    public void Name_Updates_Constellation_By_Id()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(100, 200);
        var service = new StarsCommandService(journal);

        service.Name(record.Id.ToString(), "New Name");

        Assert.Equal("New Name", journal.Constellations.Single().Name);
    }

    [Fact]
    public void Delete_Removes_Constellation_By_Name()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(100, 200);
        journal.Replace(record with { Name = "Old Name" });

        new StarsCommandService(journal).Delete("Old Name");

        Assert.Empty(journal.Constellations);
    }

    [Fact]
    public void Mutating_Commands_Invoke_Change_Callback()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(100, 200);
        var changes = 0;
        var service = new StarsCommandService(journal, () => changes++);

        service.Name(record.Id.ToString(), "Saved Name");

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Connect_Adds_Segment_And_Selects_Merged_Constellation()
    {
        var journal = new ConstellationJournal();
        var changes = 0;
        var service = new StarsCommandService(journal, () => changes++);

        var output = service.Connect("100", "200");

        var record = Assert.Single(journal.Constellations);
        Assert.True(record.Edges.Single().Matches(100, 200));
        Assert.Contains($"#{record.Id}", output);
        Assert.Equal(service.Info("selected"), service.Info(record.Id.ToString()));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Connect_Rejects_Invalid_Star_Ids()
    {
        var journal = new ConstellationJournal();
        var service = new StarsCommandService(journal);

        var output = service.Connect("100", "bad");

        Assert.Equal("Guide star HIP IDs must be whole numbers.", output);
        Assert.Empty(journal.Constellations);
    }

    [Fact]
    public void Build_Creates_Named_Authored_Constellation_From_Guide_Group()
    {
        var journal = new ConstellationJournal();
        var changes = 0;
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(24436, 0, 0, 1, null, true),
                new StarCatalogEntry(25336, 10, 0, 1, null, true),
                new StarCatalogEntry(25930, 20, 0, 1, null, true),
                new StarCatalogEntry(26311, 30, 0, 1, null, true),
                new StarCatalogEntry(26727, 40, 0, 1, null, true),
                new StarCatalogEntry(27366, 50, 0, 1, null, true),
                new StarCatalogEntry(27989, 60, 0, 1, null, true)
            ],
            [new GuideStarGroup("Ori", "Orion", [24436, 25336, 25930, 26311, 26727, 27366, 27989])],
            [BuildTestSkyCulture(new SkyCultureConstellation("Ori", "Orion", [[24436, 25336, 26727, 25930, 26311, 24436, 27366, 27989, 26727]]))]);
        var service = new StarsCommandService(journal, () => changes++, catalog);

        var output = service.Build("Orion");

        var record = Assert.Single(journal.Constellations);
        Assert.Equal("Orion", record.Name);
        Assert.Equal(8, record.Edges.Count);
        Assert.Contains(record.Edges, edge => edge.Matches(26311, 25930));
        Assert.Contains(record.Edges, edge => edge.Matches(25930, 26727));
        Assert.Contains(record.Edges, edge => edge.Matches(27366, 27989));
        Assert.Contains("Built Orion (Ori)", output);
        Assert.Contains("segments=8", output);
        Assert.Equal(service.Info("selected"), service.Info(record.Id.ToString()));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Build_Creates_Ursa_Minor_From_Authored_Template()
    {
        var journal = new ConstellationJournal();
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(11767, 0, 0, 1, null, true),
                new StarCatalogEntry(72607, 10, 0, 1, null, true),
                new StarCatalogEntry(75097, 20, 0, 1, null, true),
                new StarCatalogEntry(77055, 30, 0, 1, null, true),
                new StarCatalogEntry(79822, 40, 0, 1, null, true),
                new StarCatalogEntry(82080, 50, 0, 1, null, true),
                new StarCatalogEntry(85822, 60, 0, 1, null, true)
            ],
            [new GuideStarGroup("UMi", "Ursa Minor", [11767, 72607, 75097, 77055, 79822, 82080, 85822])],
            [BuildTestSkyCulture(new SkyCultureConstellation("UMi", "Ursa Minor", [[11767, 85822, 82080, 77055, 79822, 75097, 72607, 77055]]))]);
        var service = new StarsCommandService(journal, catalog: catalog);

        var output = service.Build("UMi");

        var record = Assert.Single(journal.Constellations);
        Assert.Equal("Ursa Minor", record.Name);
        Assert.Contains(record.Edges, edge => edge.Matches(11767, 85822));
        Assert.Contains(record.Edges, edge => edge.Matches(72607, 77055));
        Assert.Contains("segments=7", output);
    }

    [Fact]
    public void Build_Uses_Sky_Culture_Template_Without_Guide_Group()
    {
        var journal = new ConstellationJournal();
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(8832, 0, 0, 1, null, true),
                new StarCatalogEntry(8903, 10, 0, 1, null, true),
                new StarCatalogEntry(9884, 20, 0, 1, null, true),
                new StarCatalogEntry(13209, 30, 0, 1, null, false)
            ],
            [],
            [BuildTestSkyCulture(new SkyCultureConstellation("Ari", "Aries", [[8832, 8903, 9884, 13209]]))]);
        var service = new StarsCommandService(journal, catalog: catalog);

        var output = service.Build("Ari");

        var record = Assert.Single(journal.Constellations);
        Assert.Equal("Aries", record.Name);
        Assert.Equal(3, record.Edges.Count);
        Assert.Contains(record.Edges, edge => edge.Matches(9884, 13209));
        Assert.Contains("Built Aries (Ari)", output);
    }

    [Fact]
    public void Build_Can_Target_Specific_Sky_Culture()
    {
        var journal = new ConstellationJournal();
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(1, 0, 0, 1, null, true),
                new StarCatalogEntry(2, 10, 0, 1, null, true),
                new StarCatalogEntry(3, 20, 0, 1, null, true)
            ],
            [],
            [
                BuildTestSkyCulture("first", new SkyCultureConstellation("Ori", "First Orion", [[1, 2]])),
                BuildTestSkyCulture("second", new SkyCultureConstellation("Ori", "Second Orion", [[2, 3]]))
            ]);
        var service = new StarsCommandService(journal, catalog: catalog);

        var output = service.Build("second:Ori");

        var record = Assert.Single(journal.Constellations);
        Assert.Equal("Second Orion", record.Name);
        Assert.True(record.Edges.Single().Matches(2, 3));
        Assert.Contains("Built Second Orion (Ori)", output);
    }

    [Fact]
    public void Build_Reports_Missing_Authored_Group()
    {
        var service = new StarsCommandService(new ConstellationJournal(), catalog: new StarCatalog([], []));

        var output = service.Build("Missing");

        Assert.Equal("Authored constellation not found: Missing", output);
    }

    [Fact]
    public void Build_Reports_Usage_For_Empty_Target()
    {
        var service = new StarsCommandService(new ConstellationJournal(), catalog: new StarCatalog([], []));

        var output = service.Build(" ");

        Assert.Contains("Usage: .stars build <iau|name|culture:id>", output);
    }

    [Fact]
    public void Info_Includes_Rich_Calendar_Readout()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((100, 200), (200, 300));
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(100, 0, 0, 1, null, true),
                new StarCatalogEntry(200, 30, 10, 1, null, true),
                new StarCatalogEntry(300, 60, 20, 1, null, true)
            ],
            []);
        var service = new StarsCommandService(journal, catalog: catalog, latitudeDeg: 45, dayOfYear: 0, hourAfterSunset: 2);

        var output = service.Info(record.Id.ToString());

        Assert.Contains($"#{record.Id}", output);
        Assert.Contains("Unnamed Constellation", output);
        Assert.Contains("stars=3", output);
        Assert.Contains("segments=2", output);
        Assert.Contains("window=", output);
        Assert.Contains("season=", output);
        Assert.Contains("state=", output);
    }

    private static SkyCultureConstellationSet BuildTestSkyCulture(params SkyCultureConstellation[] constellations)
    {
        return BuildTestSkyCulture("test", constellations);
    }

    private static SkyCultureConstellationSet BuildTestSkyCulture(string id, params SkyCultureConstellation[] constellations)
    {
        return new SkyCultureConstellationSet(
            1,
            id,
            id,
            ["test"],
            new SkyCultureSource("test", "https://example.test", "test", "test"),
            constellations);
    }
}
