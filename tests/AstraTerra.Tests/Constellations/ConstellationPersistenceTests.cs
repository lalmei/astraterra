using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Fact]
    public void Serialized_Journal_Declares_Its_Schema_Version()
    {
        var json = ConstellationPersistence.Serialize(new ConstellationJournal());

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            ConstellationPersistence.CurrentSchemaVersion,
            document.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public void Journal_Without_A_Version_Migrates_From_The_Legacy_Schema()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(100, 200);
        var snapshot = JsonNode.Parse(ConstellationPersistence.Serialize(journal))!.AsObject();
        snapshot.Remove("version");

        var loaded = ConstellationPersistence.Deserialize(snapshot.ToJsonString());

        Assert.Equal(record.Id, loaded.Constellations.Single().Id);
    }

    [Fact]
    public void Journal_With_A_Future_Version_Is_Refused()
    {
        var snapshot = JsonNode.Parse(
            ConstellationPersistence.Serialize(new ConstellationJournal()))!.AsObject();
        snapshot["version"] = ConstellationPersistence.CurrentSchemaVersion + 1;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ConstellationPersistence.Deserialize(snapshot.ToJsonString()));

        Assert.Contains("Unsupported constellation journal version", exception.Message);
    }
}
