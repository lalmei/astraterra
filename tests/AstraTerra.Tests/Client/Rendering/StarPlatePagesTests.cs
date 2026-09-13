using System.Text.Json;
using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class StarPlatePagesTests
{
    [Fact]
    public void Every_Figure_In_The_Book_Becomes_A_Plate_In_Written_Order()
    {
        var pages = StarPlatePages.Build(
            Journal(
                Record(2, "Hare", (10, 20)),
                Record(1, "Plough", (20, 30))),
            Catalog(Star(10, 350, 5), Star(20, 0, 10), Star(30, 10, 2)));

        Assert.Equal(["Plough", "Hare"], pages.Select(page => page.Title));
    }

    [Fact]
    public void A_Plate_Draws_The_Figure_Fitted_To_The_Page()
    {
        var pages = StarPlatePages.Build(
            Journal(Record(1, "Plough", (10, 20), (20, 30))),
            Catalog(Star(10, 350, 5), Star(20, 0, 10), Star(30, 10, 2)));

        var sketch = Assert.Single(pages).Sketch;

        Assert.Equal(2, sketch.Lines.Count);
        Assert.Equal([10, 20, 30], sketch.Stars.Select(star => star.Hip));
        Assert.All(
            sketch.Stars,
            star =>
            {
                Assert.InRange(star.X, -StarPlatePages.FitRadius, StarPlatePages.FitRadius);
                Assert.InRange(star.Y, -StarPlatePages.FitRadius, StarPlatePages.FitRadius);
            });
    }

    [Fact]
    public void An_Unnamed_Figure_Is_Still_Given_A_Plate_Of_Its_Own()
    {
        var pages = StarPlatePages.Build(
            Journal(Record(7, null, (1, 2))),
            Catalog(Star(1, 0, 0), Star(2, 10, 0)));

        Assert.Contains("#7", Assert.Single(pages).Title);
    }

    [Fact]
    public void The_Caption_Counts_Stars_And_Lines_And_Credits_Whoever_Drew_It()
    {
        var pages = StarPlatePages.Build(
            Journal(Record(1, "Plough", "Tycho", (1, 2), (2, 3))),
            Catalog(Star(1, 0, 0), Star(2, 10, 0), Star(3, 20, 0)));

        Assert.Equal(["3 stars, 2 lines", "drawn by Tycho"], Assert.Single(pages).Captions);
    }

    /// <summary>
    /// An inherited figure has nobody to credit, exactly as on the written page, which says nothing
    /// rather than claiming an author it does not know.
    /// </summary>
    [Fact]
    public void A_Figure_With_No_Recorded_Author_Claims_None()
    {
        var pages = StarPlatePages.Build(
            Journal(Record(1, "Plough", (1, 2))),
            Catalog(Star(1, 0, 0), Star(2, 10, 0)));

        Assert.Equal(["2 stars, 1 line"], Assert.Single(pages).Captions);
    }

    /// <summary>
    /// A book naming stars this install cannot place still says what it holds. The plate is titled
    /// and captioned; only the drawing is missing, which is the honest report of that state.
    /// </summary>
    [Fact]
    public void A_Figure_Whose_Stars_Are_Missing_Keeps_Its_Plate_And_Draws_Nothing()
    {
        var pages = StarPlatePages.Build(Journal(Record(1, "Plough", (1, 2))), Catalog());

        var page = Assert.Single(pages);
        Assert.Equal("Plough", page.Title);
        Assert.Empty(page.Sketch.Lines);
    }

    [Fact]
    public void No_Book_And_An_Empty_Book_Have_No_Plates()
    {
        Assert.Empty(StarPlatePages.Build(null, Catalog()));
        Assert.Empty(StarPlatePages.Build(new ConstellationJournal(), Catalog()));
    }

    /// <summary>
    /// A journal the way a plate actually meets one: read back out of a written book, so records can
    /// carry the ids and authors a real book carries rather than whatever creating them here assigns.
    /// </summary>
    private static ConstellationJournal Journal(params ConstellationRecord[] records)
        => ConstellationPersistence.Deserialize(
            JsonSerializer.Serialize(
                new JournalSnapshot(
                    ConstellationPersistence.CurrentSchemaVersion,
                    records.Length + 1,
                    1,
                    1,
                    records),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static ConstellationRecord Record(int id, string? name, params (int A, int B)[] edges)
        => Record(id, name, null, edges);

    private static ConstellationRecord Record(
        int id,
        string? name,
        string? discoveredBy,
        params (int A, int B)[] edges)
        => new(
            id,
            name,
            0,
            0,
            edges.Select((edge, order) => new ConstellationEdge(edge.A, edge.B, order + 1)).ToList(),
            discoveredBy);

    private static StarCatalog Catalog(params StarCatalogEntry[] stars) => new(stars, []);

    private static StarCatalogEntry Star(int hip, double rightAscensionDeg, double declinationDeg)
        => new(hip, rightAscensionDeg, declinationDeg, 1.0, null, false);
}
