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
    public const string PlanetCatalogBookItemPath = "book-normal-red";
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

        var bookItem = api.World.GetItem(new AssetLocation("game", PlanetCatalogBookItemPath))
            ?? throw new InvalidOperationException("Cannot create the planet catalog: the normal book item is unavailable.");

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
        var bookItem = api.World.GetItem(new AssetLocation("game", bookItemPath))
            ?? throw new InvalidOperationException($"Cannot create {bookTitle}: the normal book item is unavailable.");

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

        if (catalog is null)
        {
            host.CreativeInventoryStacks = [];
            return;
        }

        try
        {
            var stacks = new List<JsonItemStack>
            {
                ToCreativeStack(CreateStarCatalog(api, catalog)),
                ToCreativeStack(CreateZodiac(api, catalog))
            };

            if (planets is not null)
            {
                stacks.Add(ToCreativeStack(CreatePlanetCatalog(api, planets)));
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
