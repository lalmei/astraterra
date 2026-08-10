using AstraTerra.Astronomy;
using Vintagestory.API.Common;

namespace AstraTerra.Constellations;

/// <summary>
/// Builds ready-made constellation journal books used by admin give commands and the creative shelf.
/// </summary>
public static class ConstellationPreparedBooks
{
    public const string StarCatalogBookId = "star-catalog";
    public const string ZodiacBookId = "zodiac";
    public const string StarCatalogBookItemPath = "book-normal-darkolive";
    public const string ZodiacBookItemPath = "book-normal-purple";
    public const string CreativeHostItemPath = "library";
    public const string CreativeTab = "astraterra";

    public static ItemStack CreateStarCatalog(ICoreAPI api, StarCatalog catalog)
        => CreatePreparedBook(
            api,
            catalog,
            ConstellationBookService.StarCatalogTitle,
            StarCatalogBookItemPath,
            StarCatalogBookId,
            c => StarCatalogJournalBuilder.Build(c));

    public static ItemStack CreateZodiac(ICoreAPI api, StarCatalog catalog)
        => CreatePreparedBook(
            api,
            catalog,
            ConstellationBookService.ZodiacTitle,
            ZodiacBookItemPath,
            ZodiacBookId,
            c => StarCatalogJournalBuilder.BuildZodiac(c));

    public static ItemStack CreatePreparedBook(
        ICoreAPI api,
        StarCatalog catalog,
        string bookTitle,
        string bookItemPath,
        string bookId,
        System.Func<StarCatalog, ConstellationJournal> buildJournal)
    {
        var bookItem = api.World.GetItem(new AssetLocation("game", bookItemPath))
            ?? throw new InvalidOperationException($"Cannot create {bookTitle}: the normal book item is unavailable.");

        var journal = buildJournal(catalog);
        var stack = new ItemStack(bookItem);
        stack.Attributes.SetString(ConstellationBookService.BookIdAttribute, bookId);
        ConstellationBookService.WriteJournal(stack, journal, bookTitle);
        return stack;
    }

    public static void RegisterCreativeStacks(ICoreAPI api, StarCatalog? catalog)
    {
        var host = api.World.GetItem(new AssetLocation("astraterra", CreativeHostItemPath));
        if (host is null)
        {
            api.Logger.Warning("AstraTerra creative host item astraterra:library is missing.");
            return;
        }

        if (catalog is null)
        {
            host.CreativeInventoryStacks = [];
            return;
        }

        try
        {
            var stacks = new[]
            {
                ToCreativeStack(CreateStarCatalog(api, catalog)),
                ToCreativeStack(CreateZodiac(api, catalog))
            };

            host.CreativeInventoryStacks =
            [
                new CreativeTabAndStackList
                {
                    Tabs = [CreativeTab],
                    Stacks = stacks
                }
            ];

            api.Logger.Event(
                "AstraTerra creative shelf registered: books={0}",
                stacks.Length);
        }
        catch (Exception exception)
        {
            host.CreativeInventoryStacks = [];
            api.Logger.Warning("AstraTerra creative shelf disabled: {0}", exception);
        }
    }

    private static JsonItemStack ToCreativeStack(ItemStack book)
        => new()
        {
            Type = EnumItemClass.Item,
            Code = book.Collectible.Code,
            ResolvedItemstack = book
        };
}
