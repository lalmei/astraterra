using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class StarCatalogJournalBuilderTests
{
    [Fact]
    public void Build_Creates_One_Named_Record_Per_Usable_Authored_Constellation()
    {
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(1, 0, 0, 1, null, true),
                new StarCatalogEntry(2, 0, 0, 1, null, true),
                new StarCatalogEntry(3, 0, 0, 1, null, true),
                new StarCatalogEntry(4, 0, 0, 1, null, true)
            ],
            [],
            [
                new SkyCultureConstellationSet(
                    1,
                    StarCatalogJournalBuilder.ModernIauCultureId,
                    "Modern IAU",
                    ["single"],
                    new SkyCultureSource("Test", "https://example.com", "Test", "Test"),
                    [
                        new SkyCultureConstellation("One", "First", [[1, 2, 3], [4, 3]]),
                        new SkyCultureConstellation("Two", "Second", [[1, 4]])
                    ])
            ]);

        var journal = StarCatalogJournalBuilder.Build(catalog);

        Assert.Equal(2, journal.Constellations.Count);
        Assert.Equal(["First", "Second"], journal.Constellations.OrderBy(record => record.Id).Select(record => record.Name));
        Assert.Equal(3, journal.Constellations.Single(record => record.Name == "First").Edges.Count);
        Assert.Single(journal.Constellations.Single(record => record.Name == "Second").Edges);
    }

    [Fact]
    public void Build_Filters_Edges_Whose_Stars_Are_Not_In_The_Catalog()
    {
        var culture = new SkyCultureConstellationSet(
            1,
            StarCatalogJournalBuilder.ModernIauCultureId,
            "Modern IAU",
            ["single"],
            new SkyCultureSource("Test", "https://example.com", "Test", "Test"),
            [new SkyCultureConstellation("One", "First", [[1, 2, 99], [99, 100]])]);
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(1, 0, 0, 1, null, true),
                new StarCatalogEntry(2, 0, 0, 1, null, true)
            ],
            [],
            [culture]);

        var journal = StarCatalogJournalBuilder.Build(catalog);

        var record = Assert.Single(journal.Constellations);
        Assert.True(Assert.Single(record.Edges).Matches(1, 2));
    }

    [Fact]
    public void Build_Reports_A_Missing_Sky_Culture()
    {
        var catalog = new StarCatalog([], [], []);

        var exception = Assert.Throws<InvalidOperationException>(() => StarCatalogJournalBuilder.Build(catalog));

        Assert.Contains(StarCatalogJournalBuilder.ModernIauCultureId, exception.Message);
    }
}
