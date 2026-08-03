using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class StarBillboardSizingTests
{
    [Theory]
    [InlineData(0.0, 1.75)]
    [InlineData(7.0, 1.75)]
    [InlineData(12.0, 3.0)]
    [InlineData(24.0, 6.0)]
    [InlineData(40.0, 6.0)]
    public void CalculateCoreDiameterPixels_Scales_To_Compact_Star_Sizes(
        double projectedSize,
        float expectedDiameter)
    {
        Assert.Equal(expectedDiameter, StarBillboardSizing.CalculateCoreDiameterPixels(projectedSize));
    }

    [Theory]
    [InlineData(-1.0, 3.0)]
    [InlineData(0.0, 3.0)]
    [InlineData(0.5, 4.5)]
    [InlineData(1.0, 6.0)]
    [InlineData(2.0, 6.0)]
    public void CalculateGlowDiameterPixels_Keeps_Brightest_Glow_Within_Twice_The_Core(
        float brightness,
        float expectedDiameter)
    {
        Assert.Equal(expectedDiameter, StarBillboardSizing.CalculateGlowDiameterPixels(3.0f, brightness));
    }
}
