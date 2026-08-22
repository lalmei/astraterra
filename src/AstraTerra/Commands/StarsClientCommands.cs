using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Config;
using AstraTerra.Constellations;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Commands;

public sealed class StarsClientCommands
{
    private readonly System.Func<ConstellationJournal, StarsCommandService> serviceFactory;
    private readonly ConstellationBookClient bookClient;
    private readonly AstraTerraConfig config;
    private readonly CometCatalog? cometCatalog;
    private int? selectedId;

    public StarsClientCommands(
        System.Func<ConstellationJournal, StarsCommandService> serviceFactory,
        ConstellationBookClient bookClient,
        AstraTerraConfig config,
        CometCatalog? cometCatalog = null)
    {
        this.serviceFactory = serviceFactory;
        this.bookClient = bookClient;
        this.config = config;
        this.cometCatalog = cometCatalog;
    }

    public void Register(ICoreClientAPI api)
    {
        api.ChatCommands.Create("stars")
            .WithDescription("Inspect and manage AstraTerra constellations.")
            .BeginSubCommand("list")
                .HandleWith(_ => TextCommandResult.Success(List()))
            .EndSubCommand()
            .BeginSubCommand("info")
                .WithArgs(api.ChatCommands.Parsers.Word("id|name"))
                .HandleWith(args => TextCommandResult.Success(Info(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("name")
                .WithArgs(api.ChatCommands.Parsers.Word("id|selected"), api.ChatCommands.Parsers.All("name"))
                .HandleWith(args =>
                {
                    var target = GetStringArg(args, 0);
                    var name = GetStringArg(args, 1);
                    return TextCommandResult.Success(Name(target, name));
                })
            .EndSubCommand()
            .BeginSubCommand("delete")
                .WithArgs(api.ChatCommands.Parsers.Word("id|name"))
                .HandleWith(args => TextCommandResult.Success(Delete(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("select")
                .WithArgs(api.ChatCommands.Parsers.Word("id|name"))
                .HandleWith(args => TextCommandResult.Success(Select(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("connect")
                .WithArgs(api.ChatCommands.Parsers.Word("hipA"), api.ChatCommands.Parsers.Word("hipB"))
                .HandleWith(args => TextCommandResult.Success(Connect(GetStringArg(args, 0), GetStringArg(args, 1))))
            .EndSubCommand()
            .BeginSubCommand("build")
                .WithArgs(api.ChatCommands.Parsers.Word("iau|name"))
                .HandleWith(args => TextCommandResult.Success(Build(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("comets")
                .WithDescription("Report every comet: whether it is up now, or how long until it returns.")
                .HandleWith(_ => TextCommandResult.Success(Comets(api)))
            .EndSubCommand()
            .BeginSubCommand("debug")
                .HandleWith(_ => TextCommandResult.Success(Debug(api)))
            .EndSubCommand()
            .BeginSubCommand("daylight-stars")
                .WithArgs(api.ChatCommands.Parsers.Word("on|off"))
                .HandleWith(args => TextCommandResult.Success(SetDaylightStars(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("starfield")
                .WithArgs(api.ChatCommands.Parsers.Word("astraterra|both|vanilla"))
                .HandleWith(args => TextCommandResult.Success(SetStarfieldMode(api, GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("sky-grid")
                .WithArgs(api.ChatCommands.Parsers.Word("none|horizontal|equatorial|both"))
                .HandleWith(args => TextCommandResult.Success(SetSkyGridMode(api, GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("render")
                .WithDescription("Switch one sky rendering path off to see what it costs. Session only.")
                .WithArgs(
                    api.ChatCommands.Parsers.OptionalWord("stars|constellations|deepsky|meteors|comets|all"),
                    api.ChatCommands.Parsers.OptionalWord("on|off"))
                .HandleWith(args => TextCommandResult.Success(SetRenderPath(api, GetStringArg(args, 0), GetStringArg(args, 1))))
            .EndSubCommand();
    }

    private string Comets(ICoreClientAPI api)
        => CometReport.Describe(
            cometCatalog,
            api.World.Calendar.TotalDays,
            Math.Max(1, api.World.Calendar.DaysPerYear));

    private string List()
    {
        if (!bookClient.HasLeftHandJournalBook())
        {
            return "Hold a writable or written constellation book in your left hand.";
        }

        return BuildService().List();
    }

    private string Info(string target)
    {
        if (!bookClient.HasLeftHandJournalBook())
        {
            return "Hold a written constellation book in your left hand.";
        }

        return BuildService().Info(ResolveSelectedTarget(target));
    }

    private string Name(string target, string name)
    {
        if (!bookClient.CanMutate(out var message))
        {
            return message;
        }

        var id = BuildService().ResolveId(ResolveSelectedTarget(target));
        if (id is null)
        {
            return $"Constellation not found: {target}";
        }

        bookClient.SendRename(id.Value, name);
        return "Constellation rename requested.";
    }

    private string Delete(string target)
    {
        if (!bookClient.CanMutate(out var message))
        {
            return message;
        }

        var id = BuildService().ResolveId(ResolveSelectedTarget(target));
        if (id is null)
        {
            return $"Constellation not found: {target}";
        }

        if (selectedId == id)
        {
            selectedId = null;
        }

        bookClient.SendDelete(id.Value);
        return "Constellation delete requested.";
    }

    private string Select(string target)
    {
        if (!bookClient.HasLeftHandJournalBook())
        {
            return "Hold a written constellation book in your left hand.";
        }

        var id = BuildService().ResolveId(target);
        if (id is null)
        {
            return $"Constellation not found: {target}";
        }

        selectedId = id;
        return $"Selected constellation #{id}.";
    }

    private string Connect(string firstHipId, string secondHipId)
    {
        if (!bookClient.CanMutate(out var message))
        {
            return message;
        }

        if (!int.TryParse(firstHipId, out var first) || !int.TryParse(secondHipId, out var second))
        {
            return "Guide star HIP IDs must be whole numbers.";
        }

        if (first == second)
        {
            return "A constellation segment needs two different guide stars.";
        }

        bookClient.SendAddEdge(first, second);
        return "Constellation connection requested.";
    }

    private string Build(string target)
    {
        if (!bookClient.CanMutate(out var message))
        {
            return message;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return "Usage: .stars build <iau|name|culture:id>. Examples: .stars build Ori, .stars build Orion, .stars build modern_iau:UMa.";
        }

        bookClient.SendBuild(target);
        return "Authored constellation build requested.";
    }

    private string Debug(ICoreClientAPI api)
    {
        var debug = BuildService().Debug();
        // Sky exposure is reported here so "I can/cannot see stars from this spot" comes back as
        // numbers rather than a screenshot to be guessed at.
        var exposure = SkyExposure.Describe(api);
        var line = $"{debug}; {exposure}";
        return selectedId is null ? line : $"{line}; selectedBookConstellation={selectedId}";
    }

    private StarsCommandService BuildService()
        => serviceFactory(bookClient.ReadCurrentJournalOrEmpty());

    private string ResolveSelectedTarget(string target)
        => string.Equals(target, "selected", StringComparison.OrdinalIgnoreCase) && selectedId is { } id
            ? id.ToString()
            : target;

    private static string SetDaylightStars(string value)
    {
        return TryParseToggle(value, out var enabled)
            ? SkyStarSunMoonRenderer.SetForceDaylightStars(enabled)
            : "Usage: .stars daylight-stars on|off";
    }

    private string SetStarfieldMode(ICoreClientAPI api, string value)
    {
        if (!StarfieldModeParser.TryParse(value, out var mode))
        {
            return "Usage: .stars starfield astraterra|both|vanilla";
        }

        config.StarfieldMode = StarfieldModeParser.ToConfigValue(mode);
        AstraTerraConfigLoader.Store(api, config);
        api.Logger.Notification("AstraTerra starfield mode changed: mode={0}", config.StarfieldMode);

        return mode switch
        {
            StarfieldMode.Both => "Starfield mode set to both: showing AstraTerra and vanilla stars.",
            StarfieldMode.Vanilla => "Starfield mode set to vanilla: showing only Vintage Story stars.",
            _ => "Starfield mode set to astraterra: showing only AstraTerra stars."
        };
    }

    private string SetSkyGridMode(ICoreClientAPI api, string value)
    {
        if (!SkyGridModeParser.TryParse(value, out var mode))
        {
            return "Usage: .stars sky-grid none|horizontal|equatorial|both";
        }

        config.SkyGridMode = SkyGridModeParser.ToConfigValue(mode);
        AstraTerraConfigLoader.Store(api, config);
        api.Logger.Notification("AstraTerra sky coordinate grid changed: mode={0}", config.SkyGridMode);

        return mode switch
        {
            SkyGridMode.Horizontal => "Sky grid set to horizontal (altitude-azimuth, cyan).",
            SkyGridMode.Equatorial => "Sky grid set to equatorial (right ascension-declination, rose).",
            SkyGridMode.Both => "Sky grids set to both: horizontal is cyan; equatorial is rose.",
            _ => "Sky coordinate grids disabled."
        };
    }

    /// <summary>
    /// Switches one rendering path off so its cost shows up as a difference in the sky-cost log line.
    /// Deliberately session-scoped and unsaved: it is a measurement tool, not a setting.
    /// </summary>
    private static string SetRenderPath(ICoreClientAPI api, string? path, string? state)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return $"Sky rendering paths: {SkyRenderPaths.Describe()}. Usage: .stars render stars|constellations|deepsky|meteors|comets|all on|off";
        }

        if (!SkyRenderPathParser.TryParse(path, out var parsedPath))
        {
            return "Usage: .stars render stars|constellations|deepsky|meteors|comets|all on|off";
        }

        if (string.IsNullOrWhiteSpace(state) || !TryParseToggle(state, out var enabled))
        {
            return "Usage: .stars render stars|constellations|deepsky|meteors|comets|all on|off";
        }

        SkyRenderPaths.Set(parsedPath, enabled);
        api.Logger.Notification("AstraTerra sky render path changed: {0}", SkyRenderPaths.Describe());

        return $"Sky rendering paths: {SkyRenderPaths.Describe()}. Costs are reported in the debug log every {SkyStarSunMoonRenderer.SkyDrawLogIntervalSeconds:0} seconds.";
    }

    private static bool TryParseToggle(string value, out bool enabled)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "on":
            case "true":
            case "yes":
            case "1":
                enabled = true;
                return true;
            case "off":
            case "false":
            case "no":
            case "0":
                enabled = false;
                return true;
            default:
                enabled = false;
                return false;
        }
    }

    private static string GetStringArg(TextCommandCallingArgs args, int index)
    {
        return index < args.ArgCount ? args[index]?.ToString() ?? string.Empty : string.Empty;
    }
}
