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
    public const string PlanetCatalogBookId = "wanderers";
    public const string StarCatalogBookItemPath = "book-normal-darkolive";
    public const string ZodiacBookItemPath = "book-normal-purple";
    public const string PlanetCatalogBookItemPath = "book-normal-cherryred";
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

    /// <summary>
    /// A book that already knows every planet by the name our own sky culture gave it.
    /// </summary>
    /// <remarks>
    /// The counterpart to identifying them one at a time through a telescope: handing an observer
    /// this book is handing them somebody else's completed work. It exists mostly so development and
    /// creative play do not have to rediscover the sky on every world.
    /// </remarks>
    public static ItemStack CreatePlanetCatalog(ICoreAPI api, PlanetCatalog planets)
    {
        ArgumentNullException.ThrowIfNull(planets);

        var bookItem = ResolveWritableBookItem(api, PlanetCatalogBookItemPath, ConstellationBookService.PlanetCatalogTitle);

        var journal = new PlanetJournal();
        foreach (var planet in planets.Planets)
        {
            journal.Rename(planet.Id, planet.DisplayName);
        }

        var stack = new ItemStack(bookItem);
        stack.Attributes.SetString(ConstellationBookService.BookIdAttribute, PlanetCatalogBookId);
        ConstellationBookService.WritePlanetJournal(stack, journal, ConstellationBookService.PlanetCatalogTitle);
        return stack;
    }

    public static ItemStack CreatePreparedBook(
        ICoreAPI api,
        StarCatalog catalog,
        string bookTitle,
        string bookItemPath,
        string bookId,
        System.Func<StarCatalog, ConstellationJournal> buildJournal)
    {
        var bookItem = ResolveWritableBookItem(api, bookItemPath, bookTitle);

        var journal = buildJournal(catalog);
        var stack = new ItemStack(bookItem);
        stack.Attributes.SetString(ConstellationBookService.BookIdAttribute, bookId);
        ConstellationBookService.WriteJournal(stack, journal, bookTitle);
        return stack;
    }

    public static void RegisterCreativeStacks(ICoreAPI api, StarCatalog? catalog)
        => RegisterCreativeStacks(api, catalog, null);

    public static void RegisterCreativeStacks(ICoreAPI api, StarCatalog? catalog, PlanetCatalog? planets)
    {
        var host = api.World.GetItem(new AssetLocation("astraterra", CreativeHostItemPath));
        if (host is null)
        {
            api.Logger.Warning("AstraTerra creative host item astraterra:library is missing.");
            return;
        }

        var stacks = new List<JsonItemStack>();

        // Both books are records of an inherited sky culture, so they have nothing to say about a
        // procedurally authored sky, where the figures are the players' to invent.
        if (catalog is not null && StarCatalogJournalBuilder.HasCulture(catalog))
        {
            TryAddCreativeBook(api, stacks, ConstellationBookService.StarCatalogTitle, () => CreateStarCatalog(api, catalog));
            TryAddCreativeBook(api, stacks, ConstellationBookService.ZodiacTitle, () => CreateZodiac(api, catalog));
        }

        if (planets is not null)
        {
            TryAddCreativeBook(
                api,
                stacks,
                ConstellationBookService.PlanetCatalogTitle,
                () => CreatePlanetCatalog(api, planets));
        }

        if (stacks.Count == 0)
        {
            host.CreativeInventoryStacks = [];
            api.Logger.Warning("AstraTerra creative shelf disabled: no prepared books could be created.");
            return;
        }

        host.CreativeInventoryStacks =
        [
            new CreativeTabAndStackList
            {
                Tabs = [CreativeTab],
                Stacks = stacks.ToArray()
            }
        ];

        api.Logger.Event(
            "AstraTerra creative shelf registered: books={0}",
            stacks.Count);
    }

    private static void TryAddCreativeBook(
        ICoreAPI api,
        List<JsonItemStack> stacks,
        string bookTitle,
        Func<ItemStack> createBook)
    {
        try
        {
            stacks.Add(ToCreativeStack(createBook()));
        }
        catch (Exception exception)
        {
            api.Logger.Warning("AstraTerra creative shelf skipped {0}: {1}", bookTitle, exception);
        }
    }

    private static Item ResolveWritableBookItem(ICoreAPI api, string preferredPath, string bookTitle)
    {
        var preferred = api.World.GetItem(new AssetLocation("game", preferredPath));
        if (preferred is not null)
        {
            return preferred;
        }

        var fallback = api.World.SearchItems(new AssetLocation("game", "book-normal-*"))
            .FirstOrDefault(item => item?.Code is not null);
        if (fallback is not null)
        {
            api.Logger.Warning(
                "AstraTerra vanilla book game:{0} is missing; using {1} for {2}.",
                preferredPath,
                fallback.Code,
                bookTitle);
            return fallback;
        }

        throw new InvalidOperationException(
            $"Cannot create {bookTitle}: vanilla book item game:{preferredPath} is unavailable.");
    }

    private static JsonItemStack ToCreativeStack(ItemStack book)
        => new()
        {
            Type = EnumItemClass.Item,
            Code = book.Collectible.Code,
            ResolvedItemstack = book
        };
}
