using System.Text.Json;
using AstraTerra.Constellations;
using AstraTerra.Observation;
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
    public void Journal_Read_Distinguishes_An_Absent_Journal_From_An_Unreadable_One()
    {
        var attributes = new TreeAttribute();

        var absent = ConstellationBookService.ReadJournalResult(attributes);

        Assert.Equal(ConstellationJournalReadStatus.Absent, absent.Status);
        Assert.Null(absent.Journal);
        Assert.Null(absent.Exception);

        const string corruptJson = "{not-json";
        attributes.SetString(ConstellationBookService.JournalJsonAttribute, corruptJson);

        var unreadable = ConstellationBookService.ReadJournalResult(attributes);

        Assert.Equal(ConstellationJournalReadStatus.Unreadable, unreadable.Status);
        Assert.Null(unreadable.Journal);
        Assert.NotNull(unreadable.Exception);
        Assert.Equal(corruptJson.Length, unreadable.JsonLength);
        Assert.Equal(corruptJson, attributes.GetString(ConstellationBookService.JournalJsonAttribute));
    }

    [Fact]
    public void WriteObservationLog_Stores_A_Dated_Ledger_Page()
    {
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteObservationLog(attributes, log);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);
        Assert.Contains("Sightings", text);
        Assert.Contains("day 412, 21:30 — alt +34° 12′", text);
        Assert.Equal(ConstellationBookService.AstraTerraSignerUid, attributes.GetString(ConstellationBookService.LockedByAttribute));

        var loaded = ConstellationBookService.ReadObservationLog(attributes);
        Assert.NotNull(loaded);
        Assert.Equal(412, loaded.Observations.Single().Day);
    }

    [Fact]
    public void WriteObservationLog_Lays_The_Comparison_Out_And_Records_The_Observers_Reading_Of_It()
    {
        var log = new ObservationLog();
        var first = log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
        var second = log.Record(34.3, 118.1, 1.4, 415, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
        log.Claim(SkyClass.Wanderer, "Ember", [first.Id, second.Id], day: 416);
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteObservationLog(attributes, log);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);
        Assert.Contains("Comparisons", text);
        Assert.Contains("Set 1: #1, #2", text);
        Assert.Contains("Findings", text);
        Assert.Contains("Ember — wandering star, from 2 sightings (#1, #2), concluded day 416", text);

        var loaded = ConstellationBookService.ReadObservationLog(attributes);
        Assert.NotNull(loaded);
        Assert.Equal("Ember", loaded.Claims.Single().Name);
    }

    [Fact]
    public void WriteObservationLog_Says_Nothing_About_What_Was_Sighted()
    {
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        var attributes = new TreeAttribute();

        ConstellationBookService.WriteObservationLog(attributes, log);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);
        Assert.DoesNotContain("Wandering", text);
        Assert.DoesNotContain("HIP", text);
    }

    [Fact]
    public void Writing_One_Half_Of_A_Book_Leaves_The_Others_On_The_Page()
    {
        var attributes = new TreeAttribute();
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        ConstellationBookService.WriteObservationLog(attributes, log);

        var planets = new PlanetJournal();
        planets.Rename("mars", "Ember");
        ConstellationBookService.WritePlanetJournal(attributes, planets);

        var journal = new ConstellationJournal();
        journal.Replace(journal.CreateFromEdge(100, 200) with { Name = "Lantern" });
        ConstellationBookService.WriteJournal(attributes, journal);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);
        Assert.Contains("Lantern", text);
        Assert.Contains("Ember", text);
        Assert.Contains("day 412", text);
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
