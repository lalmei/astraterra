using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class SkyDiscAssetTests
{
    [Fact]
    public void The_Item_Uses_The_Dedicated_Disc_Shape()
    {
        using var document = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");

        Assert.Equal(
            "astraterra:item/sky-disc",
            document.RootElement.GetProperty("shape").GetProperty("base").GetString());
    }

    [Fact]
    public void The_Shape_Has_Two_Notched_Rims_And_A_Gold_Sun_Cluster()
    {
        using var document = ReadJson("assets", "astraterra", "shapes", "item", "sky-disc.json");
        var root = document.RootElement;
        var names = root.GetProperty("elements")
            .EnumerateArray()
            .Select(element => element.GetProperty("name").GetString() ?? string.Empty)
            .ToList();

        Assert.Equal(8, names.Count(name => name.StartsWith("sunset-rim-segment-", StringComparison.Ordinal)));
        Assert.Equal(8, names.Count(name => name.StartsWith("sunrise-rim-segment-", StringComparison.Ordinal)));
        Assert.Equal(32, names.Count(name => name.StartsWith("sunset-notch-", StringComparison.Ordinal)));
        Assert.Equal(24, names.Count(name => name.StartsWith("sunrise-notch-", StringComparison.Ordinal)));
        Assert.Contains("gold-sun-centre", names);
        Assert.Equal(9, names.Count(name => name.StartsWith("gold-", StringComparison.Ordinal)));

        var textures = root.GetProperty("textures");
        Assert.Equal("game:block/metal/plate/tinbronze", textures.GetProperty("bronze").GetString());
        Assert.Equal("game:block/metal/plate/gold", textures.GetProperty("gold").GetString());
    }

    [Fact]
    public void The_Recipe_Is_A_Bronze_Plate_Between_Two_Gold_Bits()
    {
        using var document = ReadJson("assets", "astraterra", "recipes", "grid", "sky-disc.json");
        var root = document.RootElement;

        Assert.Equal("GPG", root.GetProperty("ingredientPattern").GetString());
        Assert.Equal(
            "game:metalplate-tinbronze",
            root.GetProperty("ingredients").GetProperty("P").GetProperty("code").GetString());
        Assert.Equal(
            "game:metalbit-gold",
            root.GetProperty("ingredients").GetProperty("G").GetProperty("code").GetString());
        Assert.Equal(
            "astraterra:sky-disc",
            root.GetProperty("output").GetProperty("code").GetString());
    }

    private static JsonDocument ReadJson(params string[] relativePath)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, Path.Combine(relativePath))));

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
