using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Commands;

public sealed class StarsClientCommands
{
    private readonly System.Func<ConstellationJournal, StarsCommandService> serviceFactory;
    private readonly ConstellationBookClient bookClient;
    private readonly AstraTerraConfig config;
    private readonly CometCatalog? cometCatalog;
    private readonly PlanetCatalog? planetCatalog;
    private int? selectedId;

    public StarsClientCommands(
        System.Func<ConstellationJournal, StarsCommandService> serviceFactory,
        ConstellationBookClient bookClient,
        AstraTerraConfig config,
        CometCatalog? cometCatalog = null,
        PlanetCatalog? planetCatalog = null)
    {
        this.serviceFactory = serviceFactory;
        this.bookClient = bookClient;
        this.config = config;
        this.cometCatalog = cometCatalog;
        this.planetCatalog = planetCatalog;
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
            .BeginSubCommand("sightings")
                .WithDescription("List the sightings in the book in your left hand, and what comparing them shows.")
                .HandleWith(_ => TextCommandResult.Success(Sightings()))
            .EndSubCommand()
            .BeginSubCommand("classify")
                .WithDescription("Say what a set of your own sightings was.")
                .WithArgs(
                    api.ChatCommands.Parsers.Word("set|#entry"),
                    api.ChatCommands.Parsers.Word("star|wanderer|comet"),
                    api.ChatCommands.Parsers.OptionalAll("name"))
                .HandleWith(args => TextCommandResult.Success(
                    Classify(api, GetStringArg(args, 0), GetStringArg(args, 1), GetStringArg(args, 2))))
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
            .BeginSubCommand("calendar")
                .WithDescription("Choose how much of Vintage Story's own date and hour stays on screen.")
                .WithArgs(api.ChatCommands.Parsers.Word("full|clock|none"))
                .HandleWith(args => TextCommandResult.Success(SetCalendarDisplay(api, GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("render")
                .WithDescription("Switch one sky rendering path off to see what it costs. Session only.")
                .WithArgs(
                    api.ChatCommands.Parsers.OptionalWord("stars|constellations|deepsky|meteors|comets|milkyway|all"),
                    api.ChatCommands.Parsers.OptionalWord("on|off"))
                .HandleWith(args => TextCommandResult.Success(SetRenderPath(api, GetStringArg(args, 0), GetStringArg(args, 1))))
            .EndSubCommand();
    }

    private string Sightings()
    {
        if (!bookClient.HasLeftHandJournalBook())
        {
            return "Hold a writable or written constellation book in your left hand.";
        }

        return SightingReport.Describe(bookClient.ReadCurrentObservationLogOrEmpty());
    }

    /// <summary>
    /// Records the observer's own reading of their entries. Nothing here weighs the conclusion
    /// against the sky: what the numbers show is on the page, and the call is theirs.
    /// </summary>
    private string Classify(ICoreClientAPI api, string target, string skyClass, string? name)
    {
        if (!bookClient.HasLeftHandJournalBook())
        {
            return "Hold a writable or written constellation book in your left hand.";
        }

        if (!SightingReport.TryParseClass(skyClass, out var parsedClass))
        {
            return "Say what it was: star, wanderer, or comet.";
        }

        var log = bookClient.ReadCurrentObservationLogOrEmpty();
        var entries = SightingReport.ResolveEntries(log, target, out var error);
        if (entries is null)
        {
            return error;
        }

        var planetId = parsedClass == SkyClass.Wanderer ? MatchWanderer(api, log, entries) : null;
        bookClient.SendClassifySighting(
            parsedClass,
            name,
            entries,
            (int)Math.Floor(api.World.Calendar.TotalDays),
            planetId);

        var written = string.Join(", ", entries.Select(id => $"#{id}"));
        return parsedClass == SkyClass.Wanderer && planetId is null
            ? $"Writing {written} down as a wanderer. These sightings do not settle on one body yet, "
              + "so nothing can be aimed at it — sight it again."
            : $"Writing {written} down as {skyClass.ToLowerInvariant()}...";
    }

    /// <summary>
    /// Looks up which wandering body the observer's own entries fit, once they have said it wanders.
    /// </summary>
    private string? MatchWanderer(ICoreClientAPI api, ObservationLog log, IReadOnlyList<long> entries)
    {
        if (planetCatalog is null || planetCatalog.Planets.Count == 0)
        {
            return null;
        }

        var daysPerYear = Math.Max(1, api.World.Calendar.DaysPerYear);
        var candidates = planetCatalog.Planets
            .Select(planet => (
                planet.Id,
                Ephemeris: (ISkyEphemeris)new PlanetEphemeris(planet, planetCatalog.Observer, daysPerYear)))
            .ToList();

        return SightingIdentification.Match(
            log.Observations.Where(record => entries.Contains(record.Id)),
            candidates,
            Math.Max(1.0, api.World.Calendar.HoursPerDay));
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
    /// Turns vanilla's date down, which is what makes the disc's year worth measuring. Saved,
    /// because it is a decision about the game rather than a measurement.
    /// </summary>
    private string SetCalendarDisplay(ICoreClientAPI api, string value)
    {
        if (!CalendarDisplayParser.TryParse(value, out var display))
        {
            return "Usage: .stars calendar full|clock|none";
        }

        config.CalendarDisplay = CalendarDisplayParser.ToConfigValue(display);
        AstraTerraConfigLoader.Store(api, config);
        api.Logger.Notification("AstraTerra vanilla calendar display changed: mode={0}", config.CalendarDisplay);

        return display switch
        {
            CalendarDisplay.Clock => "The character panel keeps the hour and drops the date. Reopen it to see the change.",
            CalendarDisplay.None => "The character panel gives neither the date nor the hour. Reopen it to see the change.",
            _ => "The character panel reports the date and hour as Vintage Story writes them."
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
            return $"Sky rendering paths: {SkyRenderPaths.Describe()}. Usage: .stars render stars|constellations|deepsky|meteors|comets|milkyway|all on|off";
        }

        if (!SkyRenderPathParser.TryParse(path, out var parsedPath))
        {
            return "Usage: .stars render stars|constellations|deepsky|meteors|comets|milkyway|all on|off";
        }

        if (string.IsNullOrWhiteSpace(state) || !TryParseToggle(state, out var enabled))
        {
            return "Usage: .stars render stars|constellations|deepsky|meteors|comets|milkyway|all on|off";
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
