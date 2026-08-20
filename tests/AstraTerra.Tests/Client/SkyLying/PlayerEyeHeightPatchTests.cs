using AstraTerra.Client.SkyLying.Patches;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class PlayerEyeHeightPatchTests
{
    [Fact]
    public void TargetMethod_Resolves_Vanilla_Eye_Height_Update()
    {
        var method = PlayerEyeHeightPatch.TargetMethod();

        Assert.Equal("updateEyeHeight", method.Name);
        Assert.Equal("EntityPlayer", method.DeclaringType?.Name);
        Assert.Equal([typeof(float)], method.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.False(method.IsPublic);
    }

    [Fact]
    public void Harmony_Uses_TargetMethod_Instead_Of_A_Method_Name_Annotation()
    {
        // PatchAll refuses a class that both exposes TargetMethod and names the method on
        // [HarmonyPatch]. That exception aborted client startup before the lie-down hotkey
        // was registered.
        var source = File.ReadAllText(SourcePath);

        Assert.Contains("[HarmonyTargetMethod]", source);
        Assert.DoesNotContain("[HarmonyPatch(typeof(EntityPlayer)", source);
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
            return Path.Combine(root, "src", "AstraTerra", "Client", "SkyLying", "Patches", "PlayerEyeHeightPatch.cs");
        }
    }
}
