using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class AstrolabeConstellationTargetCacheTests
{
    [Fact]
    public void Reuses_targets_until_the_catalog_book_or_journal_changes()
    {
        var catalog = Catalog(1, 2);
        var book = new object();
        var firstJournal = Journal("First figure");
        var secondJournal = Journal("Updated figure");
        var cache = new AstrolabeConstellationTargetCache();
        var reads = 0;

        var first = cache.GetOrBuild(catalog, book, "revision-1", () =>
        {
            reads++;
            return firstJournal;
        });
        var same = cache.GetOrBuild(catalog, book, "revision-1", () =>
        {
            reads++;
            return secondJournal;
        });

        Assert.Same(first, same);
        Assert.Equal("First figure", Assert.Single(first).DisplayName);
        Assert.Equal(1, reads);

        var journalUpdated = cache.GetOrBuild(catalog, book, "revision-2", () =>
        {
            reads++;
            return secondJournal;
        });

        Assert.NotSame(first, journalUpdated);
        Assert.Equal("Updated figure", Assert.Single(journalUpdated).DisplayName);
        Assert.Equal(2, reads);

        var secondBook = new object();
        var bookSwapped = cache.GetOrBuild(catalog, secondBook, "revision-2", () =>
        {
            reads++;
            return firstJournal;
        });
        Assert.NotSame(journalUpdated, bookSwapped);
        Assert.Equal(3, reads);

        var catalogReplaced = cache.GetOrBuild(Catalog(1, 3), secondBook, "revision-2", () =>
        {
            reads++;
            return secondJournal;
        });
        Assert.NotSame(bookSwapped, catalogReplaced);
        Assert.Equal(1, Assert.Single(catalogReplaced).StarCount);
        Assert.Equal(4, reads);
    }

    [Fact]
    public void Equivalent_persisted_book_ids_reuse_targets_and_removed_journal_clears_them()
    {
        var cache = new AstrolabeConstellationTargetCache();
        var catalog = Catalog(1, 2);
        var first = cache.GetOrBuild(catalog, new string('b', 4), "json", () => Journal("Figure"));
        var same = cache.GetOrBuild(catalog, new string('b', 4), "json",
            () => throw new InvalidOperationException("Warm reads must not deserialize the journal."));
        Assert.Same(first, same);

        var cleared = cache.GetOrBuild(catalog, new string('b', 4), null, () => new ConstellationJournal());
        Assert.Empty(cleared);
    }

    [Fact]
    public void Star_catalog_keeps_one_read_only_hip_index_for_all_target_builds()
    {
        var catalog = Catalog(1, 2);

        Assert.Same(catalog.StarsByHip, catalog.StarsByHip);
        Assert.Equal(1, catalog.StarsByHip[1].Hip);
        Assert.Equal(2, catalog.StarsByHip[2].Hip);
    }

    private static ConstellationJournal Journal(string name)
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(1, 2);
        journal.Replace(record with { Name = name });
        return journal;
    }

    private static StarCatalog Catalog(params int[] hips)
        => new(
            hips.Select(hip => new StarCatalogEntry(hip, hip * 10, hip, 1, null, true)).ToList(),
            []);
}
