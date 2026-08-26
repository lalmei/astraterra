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
    public void The_Disc_Can_Be_Set_Down_On_The_Ground()
    {
        using var document = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");
        var root = document.RootElement;
        var behaviour = root.GetProperty("behaviors")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == "GroundStorable");

        Assert.Equal("SingleCenter", behaviour.GetProperty("properties").GetProperty("layout").GetString());

        // Laid flat, unturned: the face the player reads is the face that looks up.
        var transform = root.GetProperty("attributes").GetProperty("groundStorageTransform");
        var rotation = transform.GetProperty("rotation");
        Assert.Equal(0, rotation.GetProperty("x").GetInt32());
        Assert.Equal(0, rotation.GetProperty("y").GetInt32());
        Assert.Equal(0, rotation.GetProperty("z").GetInt32());
        Assert.Equal(1, transform.GetProperty("scale").GetInt32());
    }

    [Fact]
    public void The_Inventory_Icon_Faces_The_Disc_Toward_The_Camera()
    {
        using var document = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");
        var rotation = document.RootElement
            .GetProperty("guiTransform")
            .GetProperty("rotation");

        // The shape lies flat in the horizontal plane, and the inventory camera looks at the
        // vertical one: leave this near zero and the icon is a disc seen edge on, which reads as an
        // item with no model at all. Negative tips the marked face toward the viewer.
        Assert.InRange(rotation.GetProperty("x").GetDouble(), -105.0, -45.0);
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
