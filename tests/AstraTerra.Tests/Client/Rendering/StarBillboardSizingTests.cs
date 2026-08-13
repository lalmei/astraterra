using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class StarBillboardSizingTests
{
    /// <summary>
    /// Angular size measured off the shipped deep-sky plates: the stars photographed into them span
    /// roughly 0.002 to 0.03 deg, and the scoped sky is drawn on top of those same plates.
    /// </summary>
    private const double WidestPlateStarDeg = 0.03;

    private const float StarAngularSizePerPixelDeg = 0.06f;
    private const float BrassTelescopeFovMultiplier = 0.12f;
    private const float PrecisionTelescopeFovMultiplier = 0.06f;

    [Fact]
    public void A_Star_Keeps_Its_Size_When_Not_Scoped()
    {
        Assert.Equal(1.0f, StarBillboardSizing.CalculateStarAngularScale(scoped: false, fovMultiplier: 0.06f));
        Assert.Equal(1.0f, StarBillboardSizing.CalculatePlanetAngularScale(scoped: false, fovMultiplier: 0.06f));
    }

    [Fact]
    public void A_Scoped_Star_Shrinks_To_The_Size_Of_The_Stars_In_The_Plates()
    {
        var brightestCoreDiameterPixels = StarBillboardSizing.MaximumCoreDiameterPixels;

        foreach (var fovMultiplier in new[] { BrassTelescopeFovMultiplier, PrecisionTelescopeFovMultiplier })
        {
            var scale = StarBillboardSizing.CalculateStarAngularScale(scoped: true, fovMultiplier);
            var angularSizeDeg = brightestCoreDiameterPixels * StarAngularSizePerPixelDeg * scale;

            // Unscaled this is 0.48 deg, which would cover a third of the Pleiades plate.
            Assert.True(
                angularSizeDeg <= WidestPlateStarDeg * 2.0,
                $"A scoped star spans {angularSizeDeg:0.0000} deg against plate stars of {WidestPlateStarDeg:0.000} deg.");
        }
    }

    [Fact]
    public void A_Scoped_Star_Holds_A_Constant_Screen_Size_As_The_Scope_Winds_In()
    {
        // Screen size goes as angular size over the field of view, so a scale that tracks the field
        // leaves the ratio flat however far the scope is wound in.
        var wide = StarBillboardSizing.CalculateStarAngularScale(scoped: true, 0.45f) / 0.45f;
        var brass = StarBillboardSizing.CalculateStarAngularScale(scoped: true, BrassTelescopeFovMultiplier) / BrassTelescopeFovMultiplier;
        var precision = StarBillboardSizing.CalculateStarAngularScale(scoped: true, PrecisionTelescopeFovMultiplier) / PrecisionTelescopeFovMultiplier;

        Assert.Equal(wide, brass, 5);
        Assert.Equal(wide, precision, 5);
    }

    [Fact]
    public void A_Scoped_Planet_Opens_Into_A_Disc_While_The_Stars_Stay_Points()
    {
        foreach (var fovMultiplier in new[] { BrassTelescopeFovMultiplier, PrecisionTelescopeFovMultiplier })
        {
            var star = StarBillboardSizing.CalculateStarAngularScale(scoped: true, fovMultiplier);
            var planet = StarBillboardSizing.CalculatePlanetAngularScale(scoped: true, fovMultiplier);

            Assert.True(planet > star * 3.0, $"A planet at field {fovMultiplier} is only {planet / star:0.0} times a star.");
        }

        // Wound all the way in, a planet grows on screen; a star does not.
        var wideOnScreen = StarBillboardSizing.CalculatePlanetAngularScale(scoped: true, 0.45f) / 0.45f;
        var zoomedOnScreen = StarBillboardSizing.CalculatePlanetAngularScale(scoped: true, PrecisionTelescopeFovMultiplier)
            / PrecisionTelescopeFovMultiplier;

        Assert.True(zoomedOnScreen > wideOnScreen * 5.0);
    }

    [Theory]
    [InlineData(0.0, 1.75)]
    [InlineData(7.0, 2.94)]
    [InlineData(12.0, 5.04)]
    [InlineData(16.75, 7.035)]
    [InlineData(24.0, 8.0)]
    [InlineData(40.0, 8.0)]
    public void CalculateCoreDiameterPixels_Scales_To_Compact_Star_Sizes(
        double projectedSize,
        float expectedDiameter)
    {
        Assert.Equal(
            expectedDiameter,
            StarBillboardSizing.CalculateCoreDiameterPixels(projectedSize),
            precision: 3);
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
