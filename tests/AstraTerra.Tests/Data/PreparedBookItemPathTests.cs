using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class PreparedBookItemPathTests
{
    [Theory]
    [InlineData(ConstellationPreparedBooks.StarCatalogBookItemPath)]
    [InlineData(ConstellationPreparedBooks.ZodiacBookItemPath)]
    [InlineData(ConstellationPreparedBooks.PlanetCatalogBookItemPath)]
    public void Prepared_Books_Use_Vanilla_Writable_Book_Variants(string bookItemPath)
    {
        Assert.StartsWith("book-normal-", bookItemPath, StringComparison.Ordinal);

        var variant = bookItemPath["book-".Length..];
        var bookJson = File.ReadAllText(LocateVanillaBookJson());
        Assert.Contains($"\"{variant}\"", bookJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Planet_Catalog_Uses_Cherryred_Instead_Of_Retired_Red()
    {
        Assert.Equal("book-normal-cherryred", ConstellationPreparedBooks.PlanetCatalogBookItemPath);
    }

    private static string LocateVanillaBookJson()
    {
        var vintageStory = Environment.GetEnvironmentVariable("VINTAGE_STORY");
        var candidates = new[]
        {
            vintageStory is null ? null : Path.Combine(vintageStory, "assets/survival/itemtypes/lore/book.json"),
            "/Applications/Vintage Story.app/assets/survival/itemtypes/lore/book.json"
        };

        var path = candidates.FirstOrDefault(candidate => candidate is not null && File.Exists(candidate));
        Assert.True(path is not null, "Vanilla book.json was not found. Set VINTAGE_STORY or install Vintage Story.");
        return path!;
    }
}
