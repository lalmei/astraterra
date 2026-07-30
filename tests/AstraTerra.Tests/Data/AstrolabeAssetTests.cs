using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class AstrolabeAssetTests
{
    [Fact]
    public void Calibrated_Astrolabe_Reuses_The_Vanilla_Model()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot, "assets/astraterra/itemtypes/calibrated-astrolabe.json")));
        var root = document.RootElement;

        Assert.Equal("calibrated-astrolabe", root.GetProperty("code").GetString());
        Assert.Equal("AstraTerra.Items.ItemAstrolabe", root.GetProperty("class").GetString());
        Assert.Equal("game:block/clutter/astrolabe", root.GetProperty("shape").GetProperty("base").GetString());
    }

    [Fact]
    public void Calibration_Recipe_Consumes_Only_The_Vanilla_Astrolabe_Clutter_Type()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot, "assets/astraterra/recipes/grid/calibrated-astrolabe.json")));
        var ingredients = document.RootElement.GetProperty("ingredients");
        var astrolabe = ingredients.GetProperty("A");
        var output = document.RootElement.GetProperty("output");

        Assert.Equal("block", astrolabe.GetProperty("type").GetString());
        Assert.Equal("game:clutter", astrolabe.GetProperty("code").GetString());
        Assert.Equal("astrolabe", astrolabe.GetProperty("attributes").GetProperty("type").GetString());
        Assert.Equal("astraterra:calibrated-astrolabe", output.GetProperty("code").GetString());
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
