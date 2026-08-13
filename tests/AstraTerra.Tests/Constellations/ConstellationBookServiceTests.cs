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

    [Theory]
    [InlineData(ConstellationBookService.StarCatalogTitle)]
    [InlineData(ConstellationBookService.ZodiacTitle)]
    public void WriteJournal_Uses_Custom_Title_For_Admin_Test_Books(string bookTitle)
    {
        var journal = new ConstellationJournal();
        journal.CreateFromEdge(10, 20);
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteJournal(attributes, journal, bookTitle);

        Assert.Equal(bookTitle, attributes.GetString(ConstellationBookService.VanillaTitleAttribute));
        using var document = JsonDocument.Parse(attributes.GetString(ConstellationBookService.SkyCultureJsonAttribute));
        Assert.Equal(bookTitle, document.RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public void EmptyJournal_ReadableText_States_No_Constellations()
    {
        var text = ConstellationBookService.BuildReadableText(new ConstellationJournal());

        Assert.Equal("No constellations recorded.", text);
    }

    [Fact]
    public void WritePlanetJournal_Stores_A_Readable_Locked_Book()
    {
        var journal = new PlanetJournal();
        journal.Rename("mars", "The Red Wanderer");
        var attributes = new TreeAttribute();

        ConstellationBookService.WritePlanetJournal(attributes, journal, "The Wanderers");

        Assert.Equal("The Wanderers", attributes.GetString(ConstellationBookService.VanillaTitleAttribute));
        Assert.Equal(ConstellationBookService.AstraTerraSigner, attributes.GetString(ConstellationBookService.VanillaSignedByAttribute));
        Assert.Contains("The Red Wanderer", attributes.GetString(ConstellationBookService.VanillaTextAttribute));
        Assert.Equal("The Red Wanderer", ConstellationBookService.ReadPlanetJournal(attributes)!.DisplayName("mars"));
    }

    /// <summary>
    /// The two halves live in separate attributes precisely so one can be written without disturbing
    /// the other — a player names a planet without losing the figures they drew.
    /// </summary>
    [Fact]
    public void The_Two_Halves_Of_A_Book_Do_Not_Overwrite_Each_Other()
    {
        var constellations = new ConstellationJournal();
        constellations.Replace(constellations.CreateFromEdge(10, 20) with { Name = "Meridian Seam" });
        var planets = new PlanetJournal();
        planets.Rename("mars", "The Red Wanderer");
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteJournal(attributes, constellations, "Field Journal");
        ConstellationBookService.WritePlanetJournal(attributes, planets);

        Assert.Single(ConstellationBookService.ReadJournal(attributes)!.Constellations);
        Assert.Equal("The Red Wanderer", ConstellationBookService.ReadPlanetJournal(attributes)!.DisplayName("mars"));

        // Writing planets keeps whatever the book is already called.
        Assert.Equal("Field Journal", attributes.GetString(ConstellationBookService.VanillaTitleAttribute));

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);
        Assert.Contains("Meridian Seam", text);
        Assert.Contains("The Red Wanderer", text);
    }

    [Fact]
    public void A_Book_Written_Before_Planets_Existed_Still_Reads()
    {
        var attributes = new TreeAttribute();
        ConstellationBookService.WriteJournal(attributes, new ConstellationJournal());

        Assert.Null(ConstellationBookService.ReadPlanetJournal(attributes));
        Assert.Empty(ConstellationBookService.ReadPlanetJournalOrEmpty(null).Planets);
    }

    [Fact]
    public void Rewriting_Constellations_Keeps_The_Planets_In_The_Same_Book()
    {
        var planets = new PlanetJournal();
        planets.Rename("venus", "Evening Lamp");
        var attributes = new TreeAttribute();
        ConstellationBookService.WritePlanetJournal(attributes, planets);

        ConstellationBookService.WriteJournal(attributes, new ConstellationJournal());

        Assert.Equal("Evening Lamp", ConstellationBookService.ReadPlanetJournal(attributes)!.DisplayName("venus"));
        Assert.Contains("Evening Lamp", attributes.GetString(ConstellationBookService.VanillaTextAttribute));
    }
}
