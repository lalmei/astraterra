using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class ConstellationPersistenceTests
{
    [Fact]
    public void Journal_RoundTrips_With_Stable_Ids()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(100, 200);

        var json = ConstellationPersistence.Serialize(journal);
        var loaded = ConstellationPersistence.Deserialize(json);

        Assert.Equal(record.Id, loaded.Constellations.Single().Id);
    }
}
