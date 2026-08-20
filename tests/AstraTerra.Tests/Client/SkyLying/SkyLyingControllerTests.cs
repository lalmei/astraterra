using AstraTerra.Client.SkyLying;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class SkyLyingControllerTests
{
    [Fact]
    public void Default_Key_Is_Not_Vanilla_Offhand_Swap()
    {
        Assert.Equal(GlKeys.Z, SkyLyingController.DefaultKey);
        Assert.NotEqual(GlKeys.X, SkyLyingController.DefaultKey);
        Assert.NotEqual(GlKeys.G, SkyLyingController.DefaultKey);
    }

    [Fact]
    public void RelocateOffhandConflict_Moves_A_Leftover_X_Binding()
    {
        var hotkey = new HotKey
        {
            CurrentMapping = new KeyCombination { KeyCode = (int)GlKeys.X }
        };

        Assert.True(SkyLyingController.RelocateOffhandConflict(hotkey));
        Assert.Equal((int)GlKeys.Z, hotkey.CurrentMapping.KeyCode);
    }

    [Fact]
    public void RelocateOffhandConflict_Leaves_A_Custom_Binding_Alone()
    {
        var hotkey = new HotKey
        {
            CurrentMapping = new KeyCombination { KeyCode = (int)GlKeys.H }
        };

        Assert.False(SkyLyingController.RelocateOffhandConflict(hotkey));
        Assert.Equal((int)GlKeys.H, hotkey.CurrentMapping.KeyCode);
        Assert.False(SkyLyingController.RelocateOffhandConflict(null));
    }

    [Fact]
    public void AimAtZenith_Snaps_Pitch_And_Mouse_Pitch()
    {
        var pos = new EntityPos { Pitch = 3.14159f };
        var mousePitch = 3.14159f;

        SkyLyingController.AimAtZenith(pos, pitch => mousePitch = pitch);

        Assert.Equal(SkyLyingPolicy.LookUpPitch, pos.Pitch);
        Assert.Equal(SkyLyingPolicy.LookUpPitch, mousePitch);
    }

    [Fact]
    public void Animation_Uses_The_Supine_Clip_And_Owns_The_Arms()
    {
        var source = File.ReadAllText(ControllerSourcePath);

        Assert.Contains("TpAnimManager", source);
        Assert.Contains("SkyLyingPolicy.ShapeAnimation", source);
        Assert.Contains("ShapeAnimationHolding", source);
        Assert.Contains("ShouldAimAtZenith", source);
        Assert.Contains("EnumCameraMode.FirstPerson", source);
        Assert.Contains("UseHandsBehindHead", source);
        Assert.Contains("AimAtZenith", source);
        Assert.Contains("game.mousePitch", source);
        Assert.Contains("UseFpAnmations = false", source);
        Assert.Contains("BodyYawLimits", source);
        Assert.Contains("AngleConstraint", source);
        Assert.DoesNotContain("Weight(meta, \"Head\")", source);
        Assert.Contains("Weight(meta, \"UpperArmL\")", source);
        Assert.DoesNotContain("Animation = \"lie\"", source);
        Assert.DoesNotContain("LieAnimation", source);
    }

    private static string ControllerSourcePath
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            var root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
            return Path.Combine(root, "src", "AstraTerra", "Client", "SkyLying", "SkyLyingController.cs");
        }
    }
}
