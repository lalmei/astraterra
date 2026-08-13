using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace AstraTerra.Commands;

public sealed class StarsServerCommands
{
    private readonly Func<StarCatalog?> catalogProvider;
    private readonly Func<PlanetCatalog?> planetProvider;
    private ICoreServerAPI? api;

    public StarsServerCommands(Func<StarCatalog?> catalogProvider)
        : this(catalogProvider, () => null)
    {
    }

    public StarsServerCommands(Func<StarCatalog?> catalogProvider, Func<PlanetCatalog?> planetProvider)
    {
        this.catalogProvider = catalogProvider;
        this.planetProvider = planetProvider;
    }

    public void Register(ICoreServerAPI api)
    {
        this.api = api;
        api.ChatCommands.Create("stars")
            .WithDescription("Inspect AstraTerra status.")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(_ => TextCommandResult.Success("AstraTerra commands: /stars debug, /stars goto-lat degrees, /stars give-catalog, /stars give-zodiac. Client journal commands are available as .stars list, .stars info, .stars build, .stars connect, .stars name, .stars select, .stars delete, and .stars daylight-stars."))
            .BeginSubCommand("debug")
                .HandleWith(_ => TextCommandResult.Success(DebugStatus()))
            .EndSubCommand()
            .BeginSubCommand("goto-lat")
                .RequiresPrivilege(Privilege.tp)
                .RequiresPlayer()
                .WithArgs(api.ChatCommands.Parsers.Word("degrees"))
                .HandleWith(args => TextCommandResult.Success(GotoLatitude(args)))
            .EndSubCommand()
            .BeginSubCommand("give-catalog")
                .RequiresPrivilege(Privilege.give)
                .RequiresPlayer()
                .HandleWith(args => TextCommandResult.Success(GiveCatalog(args)))
            .EndSubCommand()
            .BeginSubCommand("give-zodiac")
                .RequiresPrivilege(Privilege.give)
                .RequiresPlayer()
                .HandleWith(args => TextCommandResult.Success(GiveZodiac(args)))
            .EndSubCommand()
            .BeginSubCommand("give-wanderers")
                .RequiresPrivilege(Privilege.give)
                .RequiresPlayer()
                .HandleWith(args => TextCommandResult.Success(GivePlanetCatalog(args)))
            .EndSubCommand();
    }

    private string DebugStatus()
    {
        var catalog = catalogProvider();
        if (catalog is null)
        {
            return "AstraTerra astronomy catalog is not loaded on this side yet. Check client-main.log for asset loading errors.";
        }

        return $"AstraTerra astronomy catalog loaded: stars={catalog.Stars.Count}; guideGroups={catalog.GuideGroups.Count}; skyCultures={catalog.SkyCultures.Count}.";
    }

    private string GotoLatitude(TextCommandCallingArgs args)
    {
        if (api is null)
        {
            return "AstraTerra server command is not initialized.";
        }

        if (!double.TryParse(args[0]?.ToString(), out var targetLatitudeDeg))
        {
            return "Usage: /stars goto-lat degrees. Example: /stars goto-lat 45";
        }

        targetLatitudeDeg = Math.Clamp(targetLatitudeDeg, -90.0, 90.0);
        var latitudeProvider = api.World.Calendar.OnGetLatitude;
        if (latitudeProvider is null)
        {
            return "Cannot locate latitude: this world did not expose a Vintage Story latitude mapping.";
        }

        var player = args.Caller.Player;
        var position = player.Entity.Pos;
        var targetZ = LatitudeTeleportPlanner.FindNearestZForLatitude(
            targetLatitudeDeg,
            position.Z,
            minZ: 0,
            maxZ: api.WorldManager.MapSizeZ - 1,
            z => LatitudeMapper.MapClimateLatitude(latitudeProvider(z)));
        var targetX = Math.Clamp(position.X, 0, api.WorldManager.MapSizeX - 1);
        var surfaceY = api.WorldManager.GetSurfacePosY((int)Math.Round(targetX), (int)Math.Round(targetZ));
        var targetY = Math.Max(position.InternalY, (surfaceY ?? api.World.SeaLevel) + 3.0);

        player.Entity.TeleportToDouble(targetX, targetY, targetZ, () => { });

        var actualLatitude = LatitudeMapper.MapClimateLatitude(latitudeProvider(targetZ));
        return $"Teleporting to latitude {actualLatitude:0.###} near target {targetLatitudeDeg:0.###}: x={targetX:0}; y={targetY:0}; z={targetZ:0}.";
    }

    private string GiveCatalog(TextCommandCallingArgs args)
        => GivePreparedBook(
            args,
            ConstellationBookService.StarCatalogTitle,
            ConstellationPreparedBooks.CreateStarCatalog);

    private string GiveZodiac(TextCommandCallingArgs args)
        => GivePreparedBook(
            args,
            ConstellationBookService.ZodiacTitle,
            ConstellationPreparedBooks.CreateZodiac);

    /// <summary>
    /// Hands over a book that already names every planet, so development and creative play can skip
    /// identifying them one at a time.
    /// </summary>
    private string GivePlanetCatalog(TextCommandCallingArgs args)
    {
        if (api is null)
        {
            return "AstraTerra server command is not initialized.";
        }

        var planets = planetProvider();
        if (planets is null)
        {
            return $"Cannot create {ConstellationBookService.PlanetCatalogTitle}: the planet catalog is not loaded.";
        }

        ItemStack stack;
        try
        {
            stack = ConstellationPreparedBooks.CreatePlanetCatalog(api, planets);
        }
        catch (Exception exception)
        {
            return $"Cannot create {ConstellationBookService.PlanetCatalogTitle}: {exception.Message}";
        }

        var named = ConstellationBookService.ReadPlanetJournalOrEmpty(stack).Planets.Count;
        if (!args.Caller.Player.InventoryManager.TryGiveItemstack(stack, true))
        {
            return $"Could not give {ConstellationBookService.PlanetCatalogTitle}: clear one inventory slot and try again.";
        }

        return $"Gave {ConstellationBookService.PlanetCatalogTitle} naming {named} planets. Put it in your left hand and the sextant and astrolabe will use those names.";
    }

    private string GivePreparedBook(
        TextCommandCallingArgs args,
        string bookTitle,
        System.Func<ICoreAPI, StarCatalog, ItemStack> createBook)
    {
        if (api is null)
        {
            return "AstraTerra server command is not initialized.";
        }

        var catalog = catalogProvider();
        if (catalog is null)
        {
            return $"Cannot create {bookTitle}: the astronomy catalog is not loaded.";
        }

        ItemStack stack;
        try
        {
            stack = createBook(api, catalog);
        }
        catch (Exception exception)
        {
            return $"Cannot create {bookTitle}: {exception.Message}";
        }

        var constellationCount = ConstellationBookService.ReadJournalOrEmpty(stack).Constellations.Count;
        if (!args.Caller.Player.InventoryManager.TryGiveItemstack(stack, true))
        {
            return $"Could not give {bookTitle}: clear one inventory slot and try again.";
        }

        return $"Gave {bookTitle} with {constellationCount} constellations. Put it in your left hand to test the sky overlay and astrolabe.";
    }
}
