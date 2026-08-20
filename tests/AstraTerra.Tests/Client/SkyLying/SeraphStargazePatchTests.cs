using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class SeraphStargazePatchTests
{
    [Fact]
    public void Patch_Adds_A_Supine_Clip_To_The_Player_Seraph()
    {
        using var stream = File.OpenRead(PatchPath);
        using var document = JsonDocument.Parse(stream);
        var patches = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, patches.Length);

        var stargaze = AssertClip(patches, "stargaze");
        var holding = AssertClip(patches, "stargaze-hold");
        Assert.Equal(1, stargaze.GetProperty("version").GetInt32());
        Assert.Equal(1, holding.GetProperty("version").GetInt32());
        AssertGroundedBack(stargaze.GetProperty("keyframes")[0].GetProperty("elements"));
        AssertGroundedBack(holding.GetProperty("keyframes")[0].GetProperty("elements"));

        var arms = stargaze.GetProperty("keyframes")[0].GetProperty("elements");
        Assert.Equal(-192.0, arms.GetProperty("UpperArmR").GetProperty("rotationZ").GetDouble());
        Assert.Equal(28.0, arms.GetProperty("UpperArmR").GetProperty("rotationX").GetDouble());
        Assert.Equal(48.0, arms.GetProperty("UpperArmL").GetProperty("rotationY").GetDouble());
        Assert.Equal(0.0, arms.GetProperty("UpperFootL").GetProperty("rotationZ").GetDouble());

        var freeArms = holding.GetProperty("keyframes")[0].GetProperty("elements");
        Assert.Equal(0.0, freeArms.GetProperty("UpperArmR").GetProperty("rotationZ").GetDouble());
        Assert.Equal(0.0, freeArms.GetProperty("UpperArmL").GetProperty("rotationZ").GetDouble());
    }

    private static JsonElement AssertClip(JsonElement[] patches, string code)
    {
        var patch = Assert.Single(patches, candidate => candidate.GetProperty("value").GetProperty("code").GetString() == code);
        Assert.Equal("game:shapes/entity/humanoid/seraph-faceless", patch.GetProperty("file").GetString());
        Assert.Equal("add", patch.GetProperty("op").GetString());
        Assert.Equal("/animations/-", patch.GetProperty("path").GetString());
        return patch.GetProperty("value");
    }

    private static void AssertGroundedBack(JsonElement elements)
    {
        var torso = elements.GetProperty("LowerTorso");
        Assert.Equal(-90.0, torso.GetProperty("rotationZ").GetDouble());
        Assert.Equal(0.0, torso.GetProperty("rotationX").GetDouble());
        Assert.Equal(0.0, torso.GetProperty("offsetX").GetDouble());
        Assert.Equal(-13.0, torso.GetProperty("offsetY").GetDouble());
        Assert.Equal(-11.0, torso.GetProperty("offsetZ").GetDouble());
    }

    private static string PatchPath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            var root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
            return Path.Combine(root, "assets", "astraterra", "patches", "seraph-stargaze.json");
        }
    }
}
