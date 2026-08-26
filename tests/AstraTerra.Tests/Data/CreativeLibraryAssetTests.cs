using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class CreativeLibraryAssetTests
{
    [Theory]
    [InlineData("brass-telescope.json")]
    [InlineData("precision-telescope.json")]
    [InlineData("sextant.json")]
    [InlineData("calibrated-astrolabe.json")]
    [InlineData("sky-disc.json")]
    public void Instrument_Items_Appear_In_The_AstraTerra_Creative_Tab(string fileName)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot, "assets/astraterra/itemtypes", fileName)));
        var creative = document.RootElement.GetProperty("creativeinventory");

        Assert.Contains("*", creative.GetProperty("general").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("*", creative.GetProperty("items").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("*", creative.GetProperty("tools").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("*", creative.GetProperty("astraterra").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public void Creative_Host_Item_Is_Excluded_From_The_Handbook()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot, "assets/astraterra/itemtypes/meta/astraterra-library.json")));
        var root = document.RootElement;

        Assert.Equal("library", root.GetProperty("code").GetString());
        Assert.True(root.GetProperty("attributes").GetProperty("handbook").GetProperty("exclude").GetBoolean());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("creativeinventoryStacks").ValueKind);
    }

    [Fact]
    public void Lang_Names_The_AstraTerra_Creative_Tab()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot, "assets/astraterra/lang/en.json")));

        Assert.Equal("AstraTerra", document.RootElement.GetProperty("game:tabname-astraterra").GetString());
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
