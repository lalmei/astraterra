using AstraTerra.Client.SkyLying.Patches;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class PlayerStargazeAnimationPatchTests
{
    [Fact]
    public void TargetMethod_Resolves_Player_Tesselation()
    {
        var method = PlayerStargazeAnimationPatch.TargetMethod();

        Assert.Equal("OnTesselation", method.Name);
        Assert.Equal("EntityPlayer", method.DeclaringType?.Name);
        Assert.True(method.GetParameters()[0].ParameterType.IsByRef);
    }

    [Fact]
    public void Harmony_Uses_TargetMethod_Instead_Of_A_Method_Name_Annotation()
    {
        var source = File.ReadAllText(SourcePath);

        Assert.Contains("[HarmonyTargetMethod]", source);
        Assert.Contains("[HarmonyPrefix]", source);
        Assert.DoesNotContain("[HarmonyPatch(typeof(EntityPlayer)", source);
        Assert.Contains("SkyLyingAnimation.EnsureSupineClip", source);
    }

    private static string SourcePath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            var root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
            return Path.Combine(root, "src", "AstraTerra", "Client", "SkyLying", "Patches", "PlayerStargazeAnimationPatch.cs");
        }
    }
}
