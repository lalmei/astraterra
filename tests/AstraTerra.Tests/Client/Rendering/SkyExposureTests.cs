using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// This gate is only for the passes that draw after terrain and therefore cannot be occluded by it.
/// It has been wrong in both directions before, so both are pinned: a player under a block with the
/// sky in view keeps it, and a player sealed away from the sky does not.
/// </summary>
public sealed class SkyExposureTests
{
    [Fact]
    public void Open_Sky_Overhead_Needs_No_Light_Check()
    {
        Assert.True(SkyExposure.CanSeeSky(rainMapHeight: 110, eyeHeight: 112, sunlightLevel: 0));
    }

    /// <summary>
    /// The case that #71 fixed and a later light-threshold gate broke again: a block overhead must
    /// not take the whole sky away.
    /// </summary>
    [Theory]
    [InlineData(20, "a canopy or porch, barely dimmed")]
    [InlineData(9, "a room with a window across it")]
    [InlineData(3, "several blocks back from a doorway")]
    [InlineData(1, "the far end of a lit corridor")]
    public void Any_Skylight_At_All_Keeps_The_Sky(int sunlightLevel, string situation)
    {
        Assert.True(
            SkyExposure.CanSeeSky(rainMapHeight: 130, eyeHeight: 112, sunlightLevel),
            $"Sky was taken away from a player {situation}.");
    }

    [Fact]
    public void Sealed_Away_From_The_Sky_Is_The_Only_Case_That_Loses_It()
    {
        // Deep in a cave, or a room with no opening: no sky is visible in any direction.
        Assert.False(SkyExposure.CanSeeSky(rainMapHeight: 140, eyeHeight: 30, sunlightLevel: 0));
        Assert.False(SkyExposure.CanSeeSky(rainMapHeight: 118, eyeHeight: 112, sunlightLevel: 0));
    }

    /// <summary>
    /// Torchlight is not skylight. The check reads the sunlight channel precisely so a lit cave is
    /// not mistaken for an open one.
    /// </summary>
    [Fact]
    public void A_Lit_Cave_Is_Still_A_Cave()
    {
        Assert.False(SkyExposure.CanSeeSky(rainMapHeight: 140, eyeHeight: 30, sunlightLevel: 0));
    }
}
