using AstraTerra.Constellations;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Constellations;

/// <summary>
/// Losing this flag looks exactly like a bug in the mod: the book simply refuses to enter the
/// off-hand slot, and every feature that reads a held journal goes quiet with no error anywhere.
/// </summary>
public sealed class BookOffhandStorageTests
{
    [Theory]
    [InlineData("book-normal-darkolive")]
    [InlineData("book-normal-purple")]
    [InlineData("book-normal-cherryred")]
    [InlineData("BOOK-NORMAL-TEAL")]
    public void The_Writable_Books_The_Journal_Uses_Are_Recognised(string path)
    {
        Assert.True(BookOffhandStorage.IsJournalBookCode(new AssetLocation("game", path)));
    }

    [Theory]
    [InlineData("game", "book-aged-orange")]
    [InlineData("game", "book-rotten-gray")]
    [InlineData("game", "paper")]
    [InlineData("astraterra", "book-normal-darkolive")]
    public void Nothing_Else_Is(string domain, string path)
    {
        Assert.False(BookOffhandStorage.IsJournalBookCode(new AssetLocation(domain, path)));
    }

    [Fact]
    public void A_Missing_Code_Is_Not_A_Book()
    {
        Assert.False(BookOffhandStorage.IsJournalBookCode(null));
    }
}
