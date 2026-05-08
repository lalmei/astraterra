using AstraTerra.Client.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Commands;

public sealed class StarsClientCommands
{
    private readonly StarsCommandService service;

    public StarsClientCommands(StarsCommandService service)
    {
        this.service = service;
    }

    public void Register(ICoreClientAPI api)
    {
        api.ChatCommands.Create("stars")
            .WithDescription("Inspect and manage AstraTerra constellations.")
            .BeginSubCommand("list")
                .HandleWith(_ => TextCommandResult.Success(service.List()))
            .EndSubCommand()
            .BeginSubCommand("info")
                .WithArgs(api.ChatCommands.Parsers.Word("id|name"))
                .HandleWith(args => TextCommandResult.Success(service.Info(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("name")
                .WithArgs(api.ChatCommands.Parsers.Word("id|selected"), api.ChatCommands.Parsers.All("name"))
                .HandleWith(args =>
                {
                    var target = GetStringArg(args, 0);
                    var name = GetStringArg(args, 1);
                    return TextCommandResult.Success(service.Name(target, name));
                })
            .EndSubCommand()
            .BeginSubCommand("delete")
                .WithArgs(api.ChatCommands.Parsers.Word("id|name"))
                .HandleWith(args => TextCommandResult.Success(service.Delete(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("select")
                .WithArgs(api.ChatCommands.Parsers.Word("id|name"))
                .HandleWith(args => TextCommandResult.Success(service.Select(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("connect")
                .WithArgs(api.ChatCommands.Parsers.Word("hipA"), api.ChatCommands.Parsers.Word("hipB"))
                .HandleWith(args => TextCommandResult.Success(service.Connect(GetStringArg(args, 0), GetStringArg(args, 1))))
            .EndSubCommand()
            .BeginSubCommand("build")
                .WithArgs(api.ChatCommands.Parsers.Word("iau|name"))
                .HandleWith(args => TextCommandResult.Success(service.Build(GetStringArg(args, 0))))
            .EndSubCommand()
            .BeginSubCommand("debug")
                .HandleWith(_ => TextCommandResult.Success(service.Debug()))
            .EndSubCommand()
            .BeginSubCommand("daylight-stars")
                .WithArgs(api.ChatCommands.Parsers.Word("on|off"))
                .HandleWith(args => TextCommandResult.Success(SetDaylightStars(GetStringArg(args, 0))))
            .EndSubCommand();
    }

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
