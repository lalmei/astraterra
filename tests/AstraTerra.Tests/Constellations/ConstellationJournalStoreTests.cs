using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class ConstellationJournalStoreTests
{
    [Fact]
    public void Store_RoundTrips_Per_World_File()
    {
        var root = Path.Combine(Path.GetTempPath(), "astraterra-tests", Guid.NewGuid().ToString("N"));
        var store = new ConstellationJournalStore(root);
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(10, 20);

        store.Save("world-a", journal);
        var loaded = store.Load("world-a");

        Assert.Equal(record.Id, loaded.Constellations.Single().Id);
    }
}
