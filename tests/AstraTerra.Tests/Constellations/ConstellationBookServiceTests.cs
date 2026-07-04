using System.Text.Json;
using AstraTerra.Constellations;
using Vintagestory.API.Datastructures;
using Xunit;

namespace AstraTerra.Tests.Constellations;

public sealed class ConstellationBookServiceTests
{
    [Fact]
    public void WriteJournal_Stores_Readable_Locked_Book_Attributes()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((100, 200), (200, 300));
        journal.Replace(record with { Name = "Lantern" });
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteJournal(attributes, journal);

        Assert.Equal(ConstellationBookService.BookTitle, attributes.GetString(ConstellationBookService.VanillaTitleAttribute));
        Assert.Equal(ConstellationBookService.AstraTerraSigner, attributes.GetString(ConstellationBookService.VanillaSignedByAttribute));
        Assert.Equal(ConstellationBookService.AstraTerraSignerUid, attributes.GetString(ConstellationBookService.VanillaSignedByUidAttribute));
        Assert.Contains("Lantern", attributes.GetString(ConstellationBookService.VanillaTextAttribute));
        Assert.Contains("segments=2", attributes.GetString(ConstellationBookService.VanillaTextAttribute));

        var loaded = ConstellationBookService.ReadJournal(attributes);
        Assert.NotNull(loaded);
        Assert.Equal("Lantern", loaded.Constellations.Single().Name);
    }

    [Fact]
    public void WriteJournal_Stores_Derived_Sky_Culture()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((10, 20), (20, 30));
        journal.Replace(record with { Name = "Crossing" });
        var attributes = new TreeAttribute();
        attributes.SetString(ConstellationBookService.BookIdAttribute, "testbook");

        ConstellationBookService.WriteJournal(attributes, journal);

        using var document = JsonDocument.Parse(attributes.GetString(ConstellationBookService.SkyCultureJsonAttribute));
        var root = document.RootElement;
        Assert.Equal("astraterra-book-testbook", root.GetProperty("id").GetString());
        var constellation = root.GetProperty("constellations").EnumerateArray().Single();
        Assert.Equal($"AT-{record.Id}", constellation.GetProperty("iauCode").GetString());
        Assert.Equal("Crossing", constellation.GetProperty("displayName").GetString());
        Assert.Equal(2, constellation.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public void EmptyJournal_ReadableText_States_No_Constellations()
    {
        var text = ConstellationBookService.BuildReadableText(new ConstellationJournal());

        Assert.Equal("No constellations recorded.", text);
    }
}
