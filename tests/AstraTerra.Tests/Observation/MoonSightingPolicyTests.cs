using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class MoonSightingPolicyTests
{
    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.0)]
    public void A_New_Moon_Above_The_Horizon_Is_Not_Sightable(double phaseBrightness)
    {
        Assert.False(MoonSightingPolicy.IsSightable(altitudeDeg: 35.0, phaseBrightness));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.1)]
    [InlineData(0.33)]
    public void A_Lit_Moon_Above_The_Horizon_Is_Sightable(double phaseBrightness)
    {
        Assert.True(MoonSightingPolicy.IsSightable(altitudeDeg: 35.0, phaseBrightness));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    public void A_Lit_Moon_At_Or_Below_The_Horizon_Is_Not_Sightable(double altitudeDeg)
    {
        Assert.False(MoonSightingPolicy.IsSightable(altitudeDeg, phaseBrightness: 0.33));
    }

    [Fact]
    public void Invalid_Calendar_Values_Do_Not_Create_A_Phantom_Target()
    {
        Assert.False(MoonSightingPolicy.IsSightable(double.NaN, phaseBrightness: 0.33));
        Assert.False(MoonSightingPolicy.IsSightable(altitudeDeg: 35.0, double.NaN));
    }
}
