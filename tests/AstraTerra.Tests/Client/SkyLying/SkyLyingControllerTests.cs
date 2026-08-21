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

    /// <summary>
    /// Lying down plays the recline first and only settles into the idle once it has run: starting
    /// the idle straight away is what made the seraph bridge into the pose instead of lying down.
    /// </summary>
    [Fact]
    public void Lying_Down_Plays_The_Recline_Before_The_Idle()
    {
        var source = File.ReadAllText(ControllerSourcePath);

        Assert.Contains("StartRecline(entity)", source);
        Assert.Contains("ShouldSettleIntoIdle", source);
        Assert.Contains("ReclineAnimation", source);
        Assert.Contains("SkyLyingPolicy.ReclineEaseInSpeed", source);

        // The recline leaves the arms out so a held instrument keeps its own pose on the way down.
        var recline = source[source.IndexOf("CreateTransition", StringComparison.Ordinal)..];
        recline = recline[..recline.IndexOf("CreateAnimation", StringComparison.Ordinal)];
        Assert.Contains("Weight(meta, \"LowerTorso\")", recline);
        Assert.DoesNotContain("Weight(meta, \"UpperArmR\")", recline);
        Assert.DoesNotContain("Weight(meta, \"Head\")", recline);
    }

    /// <summary>
    /// Standing up plays its own clip. Stopping the idle on its own eases the supine pose out,
    /// which is the same rigid slide as reclining into it -- and the rise has to be stopped again
    /// afterwards, because it suppresses the default animations while it runs.
    /// </summary>
    [Fact]
    public void Standing_Up_Plays_The_Rise_And_Then_Takes_It_Off()
    {
        var source = File.ReadAllText(ControllerSourcePath);

        Assert.Contains("StartRise(entity)", source);
        Assert.Contains("ShouldPlayRise", source);
        Assert.Contains("ShouldFinishRising", source);
        Assert.Contains("StandUp(voluntary: true)", source);
        Assert.Contains("StandUp(voluntary: false)", source);

        // Lying down again mid-rise has to cancel it, or the two transitions fight.
        var recline = source[source.IndexOf("private void StartRecline", StringComparison.Ordinal)..];
        Assert.Contains("StopRise(entity)", recline[..recline.IndexOf("private long Elapsed", StringComparison.Ordinal)]);
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
