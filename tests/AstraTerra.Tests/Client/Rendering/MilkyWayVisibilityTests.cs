using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The band is surface brightness, so what is pinned here is that it goes before the stars do: in
/// twilight, and under a moon. A Milky Way that survives either reads as a decal.
/// </summary>
public sealed class MilkyWayVisibilityTests
{
    [Fact]
    public void A_Dark_Moonless_Night_Draws_The_Band_At_Full_Strength()
    {
        var opacity = MilkyWayVisibility.CalculateOpacity(darkness: 1.0, moonPhaseBrightness: 0.0, configuredBrightness: 1.0);

        Assert.Equal(MilkyWayVisibility.MaximumOpacity, opacity, precision: 4);
    }

    [Fact]
    public void Twilight_Has_No_Band_At_All()
    {
        var opacity = MilkyWayVisibility.CalculateOpacity(
            darkness: MilkyWayVisibility.TwilightFloor,
            moonPhaseBrightness: 0.0,
            configuredBrightness: 1.0);

        Assert.Equal(0f, opacity);
    }

    [Fact]
    public void The_Band_Fades_In_As_The_Night_Deepens()
    {
        var early = MilkyWayVisibility.CalculateOpacity(0.7, 0.0, 1.0);
        var later = MilkyWayVisibility.CalculateOpacity(0.85, 0.0, 1.0);

        Assert.True(early > 0f);
        Assert.True(later > early);
    }

    [Fact]
    public void A_Full_Moon_Takes_Most_Of_It()
    {
        var moonless = MilkyWayVisibility.CalculateOpacity(1.0, 0.0, 1.0);
        var fullMoon = MilkyWayVisibility.CalculateOpacity(1.0, 1.0, 1.0);

        Assert.Equal(moonless * (1f - (float)MilkyWayVisibility.FullMoonWashout), fullMoon, precision: 4);
    }

    [Fact]
    public void Switching_It_Off_In_The_Config_Leaves_Nothing_To_Draw()
    {
        Assert.Equal(0f, MilkyWayVisibility.CalculateOpacity(1.0, 0.0, configuredBrightness: 0.0));
    }

    [Fact]
    public void A_Raised_Setting_Cannot_Push_The_Band_Past_Opaque()
    {
        var opacity = MilkyWayVisibility.CalculateOpacity(1.0, 0.0, configuredBrightness: 50.0);

        Assert.Equal(1f, opacity);
    }
}
