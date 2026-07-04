using AstraTerra.Client.Rendering;
using AstraTerra.Constellations;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Commands;

public sealed class StarsClientCommands
{
    private readonly System.Func<ConstellationJournal, StarsCommandService> serviceFactory;
    private readonly ConstellationBookClient bookClient;
    private int? selectedId;

    public StarsClientCommands(
        System.Func<ConstellationJournal, StarsCommandService> serviceFactory,
        ConstellationBookClient bookClient)
    {
        this.serviceFactory = serviceFactory;
        this.bookClient = bookClient;
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
            .BeginSubCommand("debug")
                .HandleWith(_ => TextCommandResult.Success(Debug()))
            .EndSubCommand()
            .BeginSubCommand("daylight-stars")
                .WithArgs(api.ChatCommands.Parsers.Word("on|off"))
                .HandleWith(args => TextCommandResult.Success(SetDaylightStars(GetStringArg(args, 0))))
            .EndSubCommand();
    }

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

    private string Debug()
    {
        var debug = BuildService().Debug();
        return selectedId is null ? debug : $"{debug}; selectedBookConstellation={selectedId}";
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
