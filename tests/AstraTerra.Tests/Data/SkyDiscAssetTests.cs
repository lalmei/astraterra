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
    public void The_Shape_Is_A_Round_Disc_With_One_Rim_And_Nothing_Else_On_It()
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

        // Bare bronze: a disc carries the marks its owner cut and nothing else. Anything shipped on
        // it — a sun, its rays — is decoration nobody on the disc is responsible for, and it reads
        // as a mark somebody made.
        Assert.DoesNotContain(names, name => name.StartsWith("gold-", StringComparison.Ordinal));

        // The one exception, and a placeholder: the figure engraved in the middle. It stands in for
        // the constellation its owner draws until that figure is cut as real geometry.
        Assert.Contains("figure-plate", names);

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

        // Flat on the disc's own face and inside the rim, so the figure reads as laid into the metal
        // rather than sitting on it or spilling over the edge.
        var plate = elements.Single(element => element.GetProperty("name").GetString() == "figure-plate");
        var plateFrom = plate.GetProperty("from").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        var plateTo = plate.GetProperty("to").EnumerateArray().Select(value => value.GetDouble()).ToArray();

        Assert.Equal(0.8, plateFrom[1], 3);
        Assert.True(plateTo[1] - plateFrom[1] < 0.05, "The figure is engraved, not stuck on.");
        Assert.Equal(8.0 - plateFrom[0], plateTo[0] - 8.0, 3);
        Assert.Equal(8.0 - plateFrom[2], plateTo[2] - 8.0, 3);

        // Its corners have to clear the rim's inner face, or the figure runs under the rim.
        var rimInner = elements
            .Where(element => (element.GetProperty("name").GetString() ?? string.Empty)
                .StartsWith("sunrise-rim-segment-", StringComparison.Ordinal))
            .Min(segment => Radius(segment, nearest: true));
        var plateCorner = Math.Sqrt(((8.0 - plateFrom[0]) * (8.0 - plateFrom[0])) + ((8.0 - plateFrom[2]) * (8.0 - plateFrom[2])));
        Assert.True(plateCorner <= rimInner + 0.5, $"The figure plate reaches {plateCorner}, past a rim at {rimInner}.");

        var textures = root.GetProperty("textures");
        Assert.Equal("astraterra:item/sky-disc-figure", textures.GetProperty("figure").GetString());
        Assert.Equal([32, 32], root.GetProperty("textureSizes").GetProperty("figure").EnumerateArray().Select(value => value.GetInt32()));
        Assert.True(
            File.Exists(Path.Combine(RepositoryRoot, "assets/astraterra/textures/item/sky-disc-figure.png")),
            "The figure texture the shape names is not shipped.");

        Assert.Equal("game:block/metal/plate/tinbronze", textures.GetProperty("bronze").GetString());

        // Nothing on the shape is gold any more, but the code stays declared: a face can only use a
        // texture the shape names, and inlaying gold into a mark is the disc owner's own later work.
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
            .ToList();

        Assert.NotEmpty(rim);
        Assert.InRange(inner, rim.Min(segment => Radius(segment, nearest: true)), rim.Max(segment => Radius(segment, nearest: false)));

        // And stops inside the disc at every bearing. The body is stepped, so its edge is closer to
        // the centre at some bearings than others, and the closest of them is the one that counts.
        var body = elements
            .Where(element => (element.GetProperty("name").GetString() ?? string.Empty)
                .StartsWith("body-", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(body);
        Assert.True(
            inner + longest < body.Min(row => Radius(row, nearest: true)),
            $"A mark reaching {inner + longest} runs off a disc whose nearest edge is at {body.Min(row => Radius(row, nearest: true))}.");
    }

    /// <summary>The value of a <c>private const double</c> on a source file, by name.</summary>
    private static double Constant(string source, string name)
    {
        var match = Regex.Match(source, @"const double " + Regex.Escape(name) + @" = (-?[\d.]+);");
        Assert.True(match.Success, $"{name} is no longer a double constant on SkyDiscMeshes.");

        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Where an element's corners actually land in the disc's plane, turned as the shape turns it.
    /// </summary>
    /// <remarks>
    /// A turn is about the element's own origin, not the disc's, so a corner's distance from the
    /// middle of the disc is not what the unturned box says it is. The rim segments are all turned,
    /// so this has to do the turn before it measures anything.
    /// </remarks>
    private static IReadOnlyList<(double X, double Z)> Corners(JsonElement element)
    {
        var from = element.GetProperty("from").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        var to = element.GetProperty("to").EnumerateArray().Select(value => value.GetDouble()).ToArray();
        var origin = element.TryGetProperty("rotationOrigin", out var authored)
            ? authored.EnumerateArray().Select(value => value.GetDouble()).ToArray()
            : [from[0], from[1], from[2]];
        var turn = element.TryGetProperty("rotationY", out var rotationY)
            ? rotationY.GetDouble() * Math.PI / 180.0
            : 0.0;

        var corners = new List<(double X, double Z)>(4);
        foreach (var x in new[] { from[0], to[0] })
        {
            foreach (var z in new[] { from[2], to[2] })
            {
                var dx = x - origin[0];
                var dz = z - origin[2];
                corners.Add((
                    origin[0] + (dx * Math.Cos(turn)) + (dz * Math.Sin(turn)) - 8.0,
                    origin[2] - (dx * Math.Sin(turn)) + (dz * Math.Cos(turn)) - 8.0));
            }
        }

        return corners;
    }

    /// <summary>How far an element reaches from the centre of the disc: nearest corner, or furthest.</summary>
    private static double Radius(JsonElement element, bool nearest)
    {
        var radii = Corners(element).Select(corner => Math.Sqrt((corner.X * corner.X) + (corner.Z * corner.Z)));
        return nearest ? radii.Min() : radii.Max();
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
    public void Every_Disc_Holds_One_Figure_But_Clay_Takes_It_Only_Before_Firing()
    {
        using var fired = ReadJson("assets", "astraterra", "itemtypes", "sky-disc.json");
        var attributes = fired.RootElement.GetProperty("attributes");

        // One figure whatever it is made of: a disc is one object with one face, not a notebook.
        Assert.Equal(1, attributes.GetProperty("engravedFiguresByType").GetProperty("*").GetInt32());
        Assert.Equal(1, SkyDiscEngraving.MaxFigures);

        // But carrying a figure and being able to take one are different questions. Fired clay holds
        // the figure it was fired with and will never take another; metal is worked cold.
        var engravable = attributes.GetProperty("engravableByType");
        Assert.False(engravable.GetProperty("*-clay").GetBoolean());
        Assert.True(engravable.GetProperty("*").GetBoolean());

        // So the clay disc that can be drawn on is the raw one, which has to be an instrument at all
        // to be raised and drawn on.
        using var raw = ReadJson("assets", "astraterra", "itemtypes", "sky-disc-clay-raw.json");
        var rawRoot = raw.RootElement;

        Assert.Equal("AstraTerra.Items.ItemSkyDisc", rawRoot.GetProperty("class").GetString());
        Assert.Equal(1, rawRoot.GetProperty("attributes").GetProperty("engravedFigures").GetInt32());
        Assert.True(rawRoot.GetProperty("attributes").GetProperty("engravable").GetBoolean());

        // And what it fires into is the disc that shows the figure.
        Assert.Equal(
            "astraterra:sky-disc-clay",
            rawRoot.GetProperty("combustibleProps").GetProperty("smeltedStack").GetProperty("code").GetString());
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
