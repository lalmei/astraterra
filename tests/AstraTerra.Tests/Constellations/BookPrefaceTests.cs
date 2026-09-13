using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Datastructures;
using Xunit;

namespace AstraTerra.Tests.Constellations;

/// <summary>
/// A prepared book's opening prose: that it is there, that it comes first, and — the point of keeping
/// it in an attribute of its own — that a later hand writing in the book does not rub it out.
/// </summary>
public sealed class BookPrefaceTests
{
    [Fact]
    public void A_Preface_Opens_The_Readable_Page_Before_Anything_Recorded()
    {
        var attributes = new TreeAttribute();
        var journal = new ConstellationJournal();
        journal.Replace(journal.CreateFromEdges((100, 200)) with { Name = "Aries" });

        ConstellationBookService.WritePreface(attributes, PreparedBookPrefaces.Zodiac);
        ConstellationBookService.WriteJournal(attributes, journal, ConstellationBookService.ZodiacTitle);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);

        Assert.StartsWith("Twelve figures", text);
        Assert.Contains("Aries", text);
        Assert.True(text.IndexOf("Twelve figures", StringComparison.Ordinal) < text.IndexOf("Aries", StringComparison.Ordinal));
    }

    /// <summary>
    /// The reason a preface is not part of the journal: every write rebuilds the whole readable page,
    /// so prose held anywhere the rebuild does not look would last exactly until the next edit.
    /// </summary>
    [Fact]
    public void Writing_In_A_Prefaced_Book_Later_Does_Not_Rub_The_Preface_Out()
    {
        var attributes = new TreeAttribute();
        var journal = new ConstellationJournal();
        journal.Replace(journal.CreateFromEdges((100, 200)) with { Name = "Aries" });

        ConstellationBookService.WritePreface(attributes, PreparedBookPrefaces.Zodiac);
        ConstellationBookService.WriteJournal(attributes, journal, ConstellationBookService.ZodiacTitle);

        journal.Replace(journal.AddEdgeAndMerge(300, 400) with { Name = "Mine" });
        ConstellationBookService.WriteJournal(attributes, journal, ConstellationBookService.ZodiacTitle);

        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, 51.0, InstrumentResolution.BrassSextantDeg);
        ConstellationBookService.WriteObservationLog(attributes, log, ConstellationBookService.ZodiacTitle);

        var text = attributes.GetString(ConstellationBookService.VanillaTextAttribute);

        Assert.StartsWith("Twelve figures", text);
        Assert.Contains("Mine", text);
        Assert.Contains("Sightings", text);
    }

    [Fact]
    public void A_Book_Nobody_Prefaced_Reads_Exactly_As_It_Did_Before()
    {
        var journal = new ConstellationJournal();
        journal.Replace(journal.CreateFromEdges((100, 200)) with { Name = "Lantern" });

        var prefaced = new TreeAttribute();
        var plain = new TreeAttribute();
        ConstellationBookService.WritePreface(prefaced, string.Empty);
        ConstellationBookService.WriteJournal(prefaced, journal);
        ConstellationBookService.WriteJournal(plain, journal);

        Assert.Null(ConstellationBookService.ReadPreface(prefaced));
        Assert.Equal(
            plain.GetString(ConstellationBookService.VanillaTextAttribute),
            prefaced.GetString(ConstellationBookService.VanillaTextAttribute));
    }

    /// <summary>
    /// The preface is set on a book before anything is written into it, so it must not try to rebuild
    /// a readable page that does not exist yet — nor stamp text onto a book this mod has not written.
    /// </summary>
    [Fact]
    public void Prefacing_An_Untouched_Book_Writes_No_Page_Into_It()
    {
        var attributes = new TreeAttribute();

        ConstellationBookService.WritePreface(attributes, PreparedBookPrefaces.Zodiac);

        Assert.Equal(PreparedBookPrefaces.Zodiac, ConstellationBookService.ReadPreface(attributes));
        Assert.False(attributes.HasAttribute(ConstellationBookService.VanillaTextAttribute));
    }

    /// <summary>
    /// What the observer actually claims. The prose is the book's argument, so the argument is worth
    /// pinning: the sun's road, reading the season off it, and whose use of it is somebody else's.
    /// </summary>
    [Fact]
    public void The_Zodiac_Preface_Says_What_The_Zodiac_Is_For()
    {
        Assert.Contains("walks eastward", PreparedBookPrefaces.Zodiac);
        Assert.Contains("season", PreparedBookPrefaces.Zodiac);
        Assert.Contains("horizon", PreparedBookPrefaces.Zodiac);
        Assert.Contains("born", PreparedBookPrefaces.Zodiac);
    }
}
