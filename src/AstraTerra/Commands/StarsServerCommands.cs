using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace AstraTerra.Commands;

public sealed class StarsServerCommands
{
    private readonly Func<StarCatalog?> catalogProvider;
    private ICoreServerAPI? api;

    public StarsServerCommands(Func<StarCatalog?> catalogProvider)
    {
        this.catalogProvider = catalogProvider;
    }

    public void Register(ICoreServerAPI api)
    {
        this.api = api;
        api.ChatCommands.Create("stars")
            .WithDescription("Inspect AstraTerra status.")
            .RequiresPrivilege(Privilege.chat)
            .HandleWith(_ => TextCommandResult.Success("AstraTerra commands: /stars debug, /stars goto-lat degrees, /stars give-catalog. Client journal commands are available as .stars list, .stars info, .stars build, .stars connect, .stars name, .stars select, .stars delete, and .stars daylight-stars."))
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
    {
        if (api is null)
        {
            return "AstraTerra server command is not initialized.";
        }

        var catalog = catalogProvider();
        if (catalog is null)
        {
            return "Cannot create Star Catalog: the astronomy catalog is not loaded.";
        }

        var bookItem = api.World.GetItem(new AssetLocation("game", "book-normal-darkolive"));
        if (bookItem is null)
        {
            return "Cannot create Star Catalog: the normal book item is unavailable.";
        }

        ConstellationJournal journal;
        try
        {
            journal = StarCatalogJournalBuilder.Build(catalog);
        }
        catch (Exception exception)
        {
            return $"Cannot create Star Catalog: {exception.Message}";
        }

        var stack = new ItemStack(bookItem);
        ConstellationBookService.WriteJournal(stack, journal, ConstellationBookService.StarCatalogTitle);
        if (!args.Caller.Player.InventoryManager.TryGiveItemstack(stack, true))
        {
            return "Could not give Star Catalog: clear one inventory slot and try again.";
        }

        return $"Gave Star Catalog with {journal.Constellations.Count} constellations. Put it in your left hand to test the sky overlay and astrolabe.";
    }
}
