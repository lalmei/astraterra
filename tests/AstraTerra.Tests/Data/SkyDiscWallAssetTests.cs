using System.Text.Json;
using AstraTerra.Blocks;
using Xunit;

namespace AstraTerra.Tests.Data;

/// <summary>
/// The block that is a disc on a wall is placed by code and never held, so nothing in the game will
/// notice if its json and that code disagree. These are what notices.
/// </summary>
public sealed class SkyDiscWallAssetTests
{
    [Fact]
    public void The_Mounting_Is_Wired_To_The_Classes_That_Place_And_Draw_It()
    {
        using var document = ReadJson("assets", "astraterra", "blocktypes", "sky-disc-wall.json");
        var root = document.RootElement;

        Assert.Equal(SkyDiscWallMount.BlockCode, root.GetProperty("code").GetString());
        Assert.Equal("AstraTerra.Blocks.BlockSkyDiscWall", root.GetProperty("class").GetString());
        Assert.Equal(
            "AstraTerra.Blocks.BlockEntitySkyDiscWall",
            root.GetProperty("entityClass").GetString());

        // The attachment behaviour picks which of the four sides is placed and takes the mounting
        // down again when the wall behind it goes. Without it a disc hangs in mid-air.
        Assert.Contains(
            root.GetProperty("behaviors").EnumerateArray(),
            behaviour => behaviour.GetProperty("name").GetString() == "HorizontalAttachable");
    }

    [Fact]
    public void The_Mounting_Has_One_Variant_Per_Wall()
    {
        using var document = ReadJson("assets", "astraterra", "blocktypes", "sky-disc-wall.json");
        var states = document.RootElement
            .GetProperty("variantgroups")
            .EnumerateArray()
            .Single(group => group.GetProperty("code").GetString() == "side")
            .GetProperty("states")
            .EnumerateArray()
            .Select(state => state.GetString())
            .ToList();

        Assert.Equal(SkyDiscWallMount.Sides.OrderBy(side => side), states.OrderBy(side => side));
    }

    [Fact]
    public void What_Is_Clicked_Is_Where_The_Disc_Was_Drawn()
    {
        // The selection box is turned by the game from the json and the disc's model is turned by
        // MountMatrix in code. If the two turn different ways the disc is drawn on one wall and
        // picked up off another, and nothing but this says so.
        using var document = ReadJson("assets", "astraterra", "blocktypes", "sky-disc-wall.json");
        var rotations = document.RootElement.GetProperty("selectionbox").GetProperty("rotateYByType");

        foreach (var side in SkyDiscWallMount.Sides)
        {
            Assert.Equal(
                SkyDiscWallMount.YawDeg(side),
                (float)rotations.GetProperty($"*-{side}").GetDouble());
        }
    }

    [Fact]
    public void The_Mounting_Is_Not_A_Thing_You_Can_Hold()
    {
        using var document = ReadJson("assets", "astraterra", "blocktypes", "sky-disc-wall.json");
        var root = document.RootElement;

        // Only a disc puts one up and only a disc comes back off one, so a block item for it would
        // be an invisible block that can be placed on any wall and holds nothing.
        Assert.Empty(root.GetProperty("drops").EnumerateArray());
        Assert.Empty(root.GetProperty("creativeinventory").EnumerateObject());
    }

    [Fact]
    public void A_Fired_Disc_Hangs_And_An_Unfired_One_Does_Not()
    {
        using var fired = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");
        Assert.True(
            fired.RootElement
                .GetProperty("attributes")
                .GetProperty(SkyDiscWallMount.MountableAttribute)
                .GetBoolean());

        // Wet clay belongs in a kiln, not on a wall.
        using var raw = ReadJson("assets", "astraterra", "itemtypes", "sky-disc-clay-raw.json");
        Assert.False(
            raw.RootElement
                .GetProperty("attributes")
                .TryGetProperty(SkyDiscWallMount.MountableAttribute, out _));
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
