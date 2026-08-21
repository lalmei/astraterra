using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class SeraphStargazePatchTests
{
    [Fact]
    public void Patch_Adds_A_Supine_Clip_To_The_Player_Seraph()
    {
        var patches = LoadPatches();
        Assert.Equal(2, patches.Length);

        var stargaze = AssertClip(patches, "stargaze");
        var holding = AssertClip(patches, "stargaze-hold");
        AssertGroundedBack(stargaze.GetProperty("keyframes")[0].GetProperty("elements"));
        AssertGroundedBack(holding.GetProperty("keyframes")[0].GetProperty("elements"));

        // Arms behind the head in the free clip, mirrored left to right; at rest in the holding one,
        // where the hands are busy with the telescope.
        var arms = stargaze.GetProperty("keyframes")[0].GetProperty("elements");
        var right = arms.GetProperty("UpperArmR");
        var left = arms.GetProperty("UpperArmL");
        Assert.True(Math.Abs(right.GetProperty("rotationZ").GetDouble()) > 90.0);
        Assert.Equal(right.GetProperty("rotationZ").GetDouble(), left.GetProperty("rotationZ").GetDouble(), 3);
        Assert.Equal(-right.GetProperty("rotationX").GetDouble(), left.GetProperty("rotationX").GetDouble(), 3);

        var freeArms = holding.GetProperty("keyframes")[0].GetProperty("elements");
        Assert.Equal(0.0, freeArms.GetProperty("UpperArmR").GetProperty("rotationZ").GetDouble());
        Assert.Equal(0.0, freeArms.GetProperty("UpperArmL").GetProperty("rotationZ").GetDouble());
    }

    /// <summary>
    /// Vintage Story logs "Shape … has mixed animation versions. This will cause incorrect animation
    /// blending." when the animations in one shape do not agree on a version, and players saw exactly
    /// that on every world load. Vanilla's seraph carries 258 animations, none of which declares a
    /// version, so anything added to that shape must not declare one either.
    /// </summary>
    [Fact]
    public void Added_Clips_Do_Not_Declare_An_Animation_Version()
    {
        foreach (var patch in LoadPatches())
        {
            var clip = patch.GetProperty("value");
            Assert.False(
                clip.TryGetProperty("version", out _),
                $"Animation '{clip.GetProperty("code").GetString()}' declares a version; the shape it is patched into does not.");
        }
    }

    /// <summary>
    /// The same trap for any future shape patch: an animation added to a vanilla shape inherits that
    /// shape's version, whatever a modelling tool wrote into the export.
    /// </summary>
    [Fact]
    public void No_Shape_Patch_Anywhere_Adds_A_Versioned_Animation()
    {
        var patchDirectory = Path.Combine(RepositoryRoot, "assets", "astraterra", "patches");

        foreach (var file in Directory.EnumerateFiles(patchDirectory, "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var patch in document.RootElement.EnumerateArray())
            {
                if (!patch.TryGetProperty("path", out var path) ||
                    !path.GetString()!.StartsWith("/animations", StringComparison.Ordinal) ||
                    !patch.TryGetProperty("value", out var value) ||
                    value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                Assert.False(
                    value.TryGetProperty("version", out _),
                    $"{Path.GetFileName(file)} adds an animation that declares its own version.");
            }
        }
    }

    private static JsonElement[] LoadPatches()
    {
        // Cloned: the elements outlive the document they were parsed from.
        using var document = JsonDocument.Parse(File.ReadAllText(PatchPath));
        return document.RootElement.EnumerateArray().Select(patch => patch.Clone()).ToArray();
    }

    private static JsonElement AssertClip(JsonElement[] patches, string code)
    {
        var patch = Assert.Single(patches, candidate => candidate.GetProperty("value").GetProperty("code").GetString() == code);
        Assert.Equal("game:shapes/entity/humanoid/seraph-faceless", patch.GetProperty("file").GetString());
        Assert.Equal("add", patch.GetProperty("op").GetString());
        Assert.Equal("/animations/-", patch.GetProperty("path").GetString());
        return patch.GetProperty("value");
    }

    /// <summary>
    /// Flat on the back: the torso is rolled a quarter turn about Z and dropped to the ground. The
    /// offsets that go with it are expressed in the rotated frame, which is what animation version 0
    /// means — see tools/convert-animation-version.py.
    /// </summary>
    private static void AssertGroundedBack(JsonElement elements)
    {
        var torso = elements.GetProperty("LowerTorso");
        Assert.Equal(-90.0, torso.GetProperty("rotationZ").GetDouble(), 3);
        Assert.Equal(0.0, torso.GetProperty("rotationX").GetDouble(), 3);
        Assert.Equal(-11.0, torso.GetProperty("offsetZ").GetDouble(), 3);
    }

    private static string PatchPath
        => Path.Combine(RepositoryRoot, "assets", "astraterra", "patches", "seraph-stargaze.json");

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
