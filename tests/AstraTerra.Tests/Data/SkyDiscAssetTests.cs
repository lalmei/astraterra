using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstraTerra.Observation;
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

        // The shape lies flat in the horizontal plane and the inventory camera looks at the vertical
        // one, so near zero draws the disc edge on — a sliver, indistinguishable from an item with
        // no model. Which way it tips matters as much: an item's transform is used as written while
        // a block's is turned a half circle first, so the angles either side of a right angle show
        // opposite faces, and a disc turned the wrong way is a blank plate with its rim, its
        // graduations and every mark on the side away from the viewer.
        Assert.InRange(rotation.GetProperty("x").GetDouble(), 75.0, 135.0);
    }

    [Fact]
    public void The_Shape_Is_A_Round_Disc_With_One_Rim_And_A_Gold_Sun()
    {
        using var document = ReadJson("assets", "astraterra", "shapes", "item", "sky-disc.json");
        var root = document.RootElement;
        var elements = root.GetProperty("elements").EnumerateArray().ToList();
        var names = elements
            .Select(element => element.GetProperty("name").GetString() ?? string.Empty)
            .ToList();

        // One rim, no graduations: the marks a disc carries are its own, and a ring of notches under
        // them read as a second set of marks nobody made.
        Assert.DoesNotContain(names, name => name.Contains("notch", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("sunset-rim-segment-", StringComparison.Ordinal));
        Assert.Equal(16, names.Count(name => name.StartsWith("sunrise-rim-segment-", StringComparison.Ordinal)));

        // The mesh builder copies this one to make a mark, so it has to be there.
        Assert.Contains("sunrise-rim-segment-01", names);

        // The sun, and nothing radiating from it: a disc carries the marks its owner cut, and a
        // fixed spray of rays is decoration nobody on the disc is responsible for.
        Assert.Equal(["gold-sun-centre"], names.Where(name => name.StartsWith("gold-", StringComparison.Ordinal)));

        // Round, not stepped: every row of the body is cut to a circle of radius seven about (8, 8),
        // to within the half unit a row's own depth allows.
        var body = elements
            .Where(element => (element.GetProperty("name").GetString() ?? string.Empty)
                .StartsWith("body-", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(14, body.Count);
        foreach (var row in body)
        {
            var from = row.GetProperty("from").EnumerateArray().Select(value => value.GetDouble()).ToArray();
            var to = row.GetProperty("to").EnumerateArray().Select(value => value.GetDouble()).ToArray();

            // Symmetric about the centre line, so the disc has no lopsided side.
            Assert.Equal(8.0 - from[0], to[0] - 8.0, 3);

            var depth = Math.Abs(((from[2] + to[2]) / 2.0) - 8.0);
            var half = (to[0] - from[0]) / 2.0;
            Assert.Equal(Math.Sqrt((7.0 * 7.0) - (depth * depth)), half, 3);
        }

        var textures = root.GetProperty("textures");
        Assert.Equal("game:block/metal/plate/tinbronze", textures.GetProperty("bronze").GetString());
        Assert.Equal("game:block/metal/plate/gold", textures.GetProperty("gold").GetString());
    }

    [Fact]
    public void A_Mark_Runs_From_The_Rim_Outwards_And_Stops_Inside_The_Disc()
    {
        // The marks are built in code against the shape authored here, so the two have to be checked
        // against each other: a mark that starts short of the rim floats, and one that runs long
        // hangs off the edge of the disc.
        var meshes = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyDiscMeshes.cs"));
        var inner = Constant(meshes, "MarkInnerRadius");
        var longest = Math.Max(Constant(meshes, "MarkLength"), Constant(meshes, "EdgeMarkLength"));

        using var document = ReadJson("assets", "astraterra", "shapes", "item", "sky-disc.json");
        var elements = document.RootElement.GetProperty("elements").EnumerateArray().ToList();

        // Every mark starts at the rim, whichever band it belongs to: inside the rim's outer face,
        // so no bare face shows between the two, and outside its inner one, so it is not on the rim.
        var rim = elements
            .Where(element => (element.GetProperty("name").GetString() ?? string.Empty)
                .StartsWith("sunrise-rim-segment-", StringComparison.Ordinal))
            .Select(Footprint)
            .ToList();

        Assert.NotEmpty(rim);
        Assert.InRange(inner, rim.Min(box => Radius(box, nearest: true)), rim.Max(box => Radius(box, nearest: false)));

        // And stops inside the disc at every bearing. The body is stepped, so its edge is closer to
        // the centre at some bearings than others, and the closest of them is the one that counts.
        var body = elements
            .Where(element => (element.GetProperty("name").GetString() ?? string.Empty)
                .StartsWith("body-", StringComparison.Ordinal))
            .Select(Footprint)
            .ToList();

        Assert.NotEmpty(body);
        Assert.True(
            inner + longest < body.Min(box => Radius(box, nearest: true)),
            $"A mark reaching {inner + longest} runs off a disc whose nearest edge is at {body.Min(box => Radius(box, nearest: true))}.");
    }

    /// <summary>The value of a <c>private const double</c> on a source file, by name.</summary>
    private static double Constant(string source, string name)
    {
        var match = Regex.Match(source, @"const double " + Regex.Escape(name) + @" = (-?[\d.]+);");
        Assert.True(match.Success, $"{name} is no longer a double constant on SkyDiscMeshes.");

        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>An element's extent in the disc's plane, as (x1, z1, x2, z2) about the centre.</summary>
    private static (double X1, double Z1, double X2, double Z2) Footprint(JsonElement element)
    {
        var from = element.GetProperty("from").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        var to = element.GetProperty("to").EnumerateArray().Select(value => value.GetDouble()).ToArray();

        return (from[0] - 8.0, from[2] - 8.0, to[0] - 8.0, to[2] - 8.0);
    }

    /// <summary>
    /// How far a box reaches from the centre of the disc: its nearest corner, or its furthest.
    /// </summary>
    /// <remarks>
    /// The rim segments are turned about their own centres, which leaves every corner the same
    /// distance out as it was, so unturned corners answer the question for a turned box too.
    /// </remarks>
    private static double Radius((double X1, double Z1, double X2, double Z2) box, bool nearest)
    {
        double[] corners =
        [
            Math.Sqrt((box.X1 * box.X1) + (box.Z1 * box.Z1)),
            Math.Sqrt((box.X2 * box.X2) + (box.Z1 * box.Z1)),
            Math.Sqrt((box.X1 * box.X1) + (box.Z2 * box.Z2)),
            Math.Sqrt((box.X2 * box.X2) + (box.Z2 * box.Z2)),
        ];

        return nearest ? corners.Min() : corners.Max();
    }

    [Fact]
    public void The_Recipe_Is_A_Plate_Of_Either_Metal_Between_Two_Gold_Bits()
    {
        using var document = ReadJson("assets", "astraterra", "recipes", "grid", "sky-disc.json");
        var root = document.RootElement;
        var plate = root.GetProperty("ingredients").GetProperty("P");

        Assert.Equal("GPG", root.GetProperty("ingredientPattern").GetString());
        Assert.Equal("game:metalplate-*", plate.GetProperty("code").GetString());
        Assert.Equal(
            ["copper", "tinbronze"],
            plate.GetProperty("allowedVariants").EnumerateArray().Select(variant => variant.GetString()));
        Assert.Equal(
            "game:metalbit-gold",
            root.GetProperty("ingredients").GetProperty("G").GetProperty("code").GetString());

        // The disc you get is made of the plate you put in.
        Assert.Equal(
            "astraterra:sky-disc-{metal}",
            root.GetProperty("output").GetProperty("code").GetString());
    }

    [Fact]
    public void The_Rougher_The_Material_The_Coarser_The_Rim()
    {
        using var document = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");
        var root = document.RootElement;

        Assert.Equal(
            ["clay", "copper", "tinbronze"],
            root.GetProperty("variantgroups")
                .EnumerateArray()
                .Single(group => group.GetProperty("code").GetString() == "material")
                .GetProperty("states")
                .EnumerateArray()
                .Select(state => state.GetString()));

        // P1: a rougher disc buys patience, not access. Same solstice, rounder answers — and metal
        // is where the rim stops getting better.
        var notches = root.GetProperty("attributes").GetProperty("arcNotchDegByType");
        Assert.Equal(SolarBandPolicy.RoughArcNotchDeg, notches.GetProperty("*-clay").GetDouble());
        Assert.Equal(SolarBandPolicy.MetalArcNotchDeg, notches.GetProperty("*").GetDouble());
    }

    [Fact]
    public void Past_Copper_What_A_Disc_Buys_Is_Room_To_Be_Engraved()
    {
        using var document = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");
        var figures = document.RootElement.GetProperty("attributes").GetProperty("engravedFiguresByType");

        // Clay holds marks, not a memory: you inlay a figure into metal, and the disc worth keeping
        // is the one that can carry the constellations you drew.
        Assert.Equal(0, figures.GetProperty("*-clay").GetInt32());
        Assert.Equal(1, figures.GetProperty("*-copper").GetInt32());
        Assert.Equal(SkyDiscEngraving.MaxFigures, figures.GetProperty("*").GetInt32());
    }

    [Fact]
    public void The_Clay_Disc_Is_Formed_And_Fired_Rather_Than_Crafted()
    {
        using var recipe = ReadJson("assets", "astraterra", "recipes", "clayforming", "sky-disc.json");
        var pattern = recipe.RootElement.GetProperty("pattern").EnumerateArray().Single();
        var rows = pattern.EnumerateArray().Select(row => row.GetString() ?? string.Empty).ToList();

        // One layer, and round: a disc is formed flat and fired flat.
        Assert.All(rows, row => Assert.Equal(rows.Count, row.Length));
        Assert.Equal(3, rows[0].Count(voxel => voxel == '#'));
        Assert.Equal(rows.Count, rows[rows.Count / 2].Count(voxel => voxel == '#'));
        Assert.Equal(
            "astraterra:sky-disc-clay-{color}-raw",
            recipe.RootElement.GetProperty("output").GetProperty("code").GetString());

        using var raw = ReadJson("assets", "astraterra", "itemtypes", "sky-disc-clay-raw.json");
        var combustible = raw.RootElement.GetProperty("combustibleProps");

        Assert.Equal("fire", combustible.GetProperty("smeltingType").GetString());
        Assert.Equal(
            "astraterra:sky-disc-clay",
            combustible.GetProperty("smeltedStack").GetProperty("code").GetString());
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
