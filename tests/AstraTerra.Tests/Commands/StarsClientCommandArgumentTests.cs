using System.Reflection;
using AstraTerra.Commands;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Commands;

/// <summary>
/// Guards the argument reader against Vintage Story's own bookkeeping: a parser that takes all the
/// rest of the line reports its argument count as -1, so summing the counts hides every argument on
/// subcommands such as <c>.stars classify</c> and <c>.stars name</c>.
/// </summary>
public sealed class StarsClientCommandArgumentTests
{
    private static readonly MethodInfo GetStringArgMethod = typeof(StarsClientCommands)
        .GetMethod("GetStringArg", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string GetStringArg(TextCommandCallingArgs args, int index)
        => (string)GetStringArgMethod.Invoke(null, [args, index])!;

    [Fact]
    public void Arguments_Before_A_Take_All_Parser_Are_Still_Readable()
    {
        var args = ArgsWith(
            Word("set|#entry", "2"),
            Word("star|wanderer|comet", "wanderer"),
            TakeAll("name", "Oakchild"));

        Assert.Equal("2", GetStringArg(args, 0));
        Assert.Equal("wanderer", GetStringArg(args, 1));
        Assert.Equal("Oakchild", GetStringArg(args, 2));
    }

    [Fact]
    public void An_Omitted_Optional_Name_Reads_As_Empty()
    {
        var args = ArgsWith(
            Word("set|#entry", "#4"),
            Word("star|wanderer|comet", "comet"),
            TakeAll("name", null));

        Assert.Equal("#4", GetStringArg(args, 0));
        Assert.Equal("comet", GetStringArg(args, 1));
        Assert.Equal(string.Empty, GetStringArg(args, 2));
    }

    [Fact]
    public void Reading_Past_The_Last_Parser_Reads_As_Empty()
    {
        var args = ArgsWith(Word("id|name", "Ember"));

        Assert.Equal(string.Empty, GetStringArg(args, 1));
    }

    private static TextCommandCallingArgs ArgsWith(params ICommandArgumentParser[] parsers)
    {
        var args = new TextCommandCallingArgs();
        args.Parsers.AddRange(parsers);
        return args;
    }

    private static ICommandArgumentParser Word(string name, string value)
    {
        var parser = new WordArgParser(name, isMandatoryArg: true);
        parser.SetValue(value);
        return parser;
    }

    private static ICommandArgumentParser TakeAll(string name, string? value)
    {
        var parser = new StringArgParser(name, isMandatoryArg: false);
        parser.SetValue(value!);
        return parser;
    }
}
