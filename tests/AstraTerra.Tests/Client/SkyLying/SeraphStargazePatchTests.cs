using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class SeraphStargazePatchTests
{
    [Fact]
    public void Patch_Adds_The_Recline_The_Rise_And_Both_Supine_Idles()
    {
        var patches = LoadPatches();
        Assert.Equal(4, patches.Length);

        var recline = AssertClip(patches, "stargaze-down");
        var rise = AssertClip(patches, "stargaze-up");
        AssertGroundedBack(FirstFrame(rise));
        var stargaze = AssertClip(patches, "stargaze");
        var holding = AssertClip(patches, "stargaze-hold");
        AssertGroundedBack(LastFrame(recline));
        AssertGroundedBack(FirstFrame(stargaze));
        AssertGroundedBack(FirstFrame(holding));

        // The recline starts standing: the engine eases a clip in by blending its first keyframe
        // against the pose the seraph is in, so a first frame that is already supine plays as a
        // slide into it -- feet planted, hips in the air.
        Assert.All(
            FirstFrame(recline).EnumerateObject(),
            element => Assert.All(
                element.Value.EnumerateObject(),
                axis => Assert.Equal(0.0, axis.Value.GetDouble(), 3)));
        Assert.Equal("Hold", recline.GetProperty("onAnimationEnd").GetString());
        Assert.Equal("Repeat", stargaze.GetProperty("onAnimationEnd").GetString());

        // Arms behind the head in the free clip, mirrored left to right; absent from the holding
        // one, where the hands are busy with an instrument, and from the recline, which a held
        // telescope has to survive.
        var arms = FirstFrame(stargaze);
        var right = arms.GetProperty("UpperArmR");
        var left = arms.GetProperty("UpperArmL");
        Assert.Equal(right.GetProperty("rotationZ").GetDouble(), left.GetProperty("rotationZ").GetDouble(), 3);
        Assert.Equal(-right.GetProperty("rotationX").GetDouble(), left.GetProperty("rotationX").GetDouble(), 3);
        Assert.False(FirstFrame(holding).TryGetProperty("UpperArmR", out _));
        Assert.False(LastFrame(recline).TryGetProperty("UpperArmR", out _));
    }

    /// <summary>
    /// Rotating a bone past its socket is what tore the elbows off the earlier pose. Vanilla's own
    /// clips stay inside about 140 degrees on any one axis; so should these.
    /// </summary>
    [Fact]
    public void No_Joint_Is_Turned_Past_Its_Socket()
    {
        foreach (var clip in LoadPatches().Select(patch => patch.GetProperty("value")))
        {
            foreach (var keyframe in clip.GetProperty("keyframes").EnumerateArray())
            {
                foreach (var element in keyframe.GetProperty("elements").EnumerateObject())
                {
                    foreach (var axis in element.Value.EnumerateObject().Where(axis => axis.Name.StartsWith("rotation", StringComparison.Ordinal)))
                    {
                        Assert.InRange(axis.Value.GetDouble(), -140.0, 140.0);
                    }
                }
            }
        }
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
        Assert.Equal(-90.0, Axis(torso, "rotationZ"), 3);
        Assert.Equal(0.0, Axis(torso, "rotationX"), 3);

        // Version 0 translates in the rotated frame, so dropping the hips of a torso pitched onto
        // its back is mostly X. It is solved, not written by hand: tools/build_stargaze_clips.py.
        Assert.True(Axis(torso, "offsetX") > 0.0);
        Assert.True(Axis(torso, "offsetY") < 0.0);
    }

    private static double Axis(JsonElement pose, string name)
        => pose.TryGetProperty(name, out var value) ? value.GetDouble() : 0.0;

    private static JsonElement FirstFrame(JsonElement clip)
        => clip.GetProperty("keyframes")[0].GetProperty("elements");

    private static JsonElement LastFrame(JsonElement clip)
    {
        var keyframes = clip.GetProperty("keyframes");
        return keyframes[keyframes.GetArrayLength() - 1].GetProperty("elements");
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
