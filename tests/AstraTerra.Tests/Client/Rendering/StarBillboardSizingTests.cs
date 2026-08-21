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

    /// <summary>
    /// How much of the screen the lit part of a star covers: its billboard, scaled by how much of
    /// that billboard its sprite actually fills, over the field of view it is seen through.
    /// </summary>
    private static double DrawnCoreScreenShare(double angularScale, double coreCoverage, double fovMultiplier)
        => StarBillboardSizing.MaximumCoreDiameterPixels
            * StarAngularSizePerPixelDeg
            * angularScale
            * coreCoverage
            / fovMultiplier;

    [Fact]
    public void A_Star_Keeps_Its_Size_When_Not_Scoped()
    {
        Assert.Equal(1.0f, StarBillboardSizing.CalculateBrightStarAngularScale(scoped: false, fovMultiplier: 0.06f));
        Assert.Equal(1.0f, StarBillboardSizing.CalculateFaintStarAngularScale(scoped: false, fovMultiplier: 0.06f));
        Assert.Equal(1.0f, StarBillboardSizing.CalculatePlanetAngularScale(scoped: false, fovMultiplier: 0.06f));
    }

    /// <summary>
    /// Raising the scope must not change how big a star looks. It used to: the scoped sprite fills
    /// less than half the quad the naked-eye one does, so at an unchanged billboard size every bright
    /// star halved the moment the telescope came up.
    /// </summary>
    [Theory]
    [InlineData(BrassTelescopeFovMultiplier)]
    [InlineData(PrecisionTelescopeFovMultiplier)]
    [InlineData(0.45f)]
    public void A_Star_Is_Drawn_The_Same_Size_Either_Side_Of_The_Sprite_Swap(float fovMultiplier)
    {
        var nakedBright = DrawnCoreScreenShare(
            StarBillboardSizing.CalculateBrightStarAngularScale(scoped: false, 1.0f),
            StarBillboardSizing.NakedEyeBrightCoreCoverage,
            1.0);
        var scopedBright = DrawnCoreScreenShare(
            StarBillboardSizing.CalculateBrightStarAngularScale(scoped: true, fovMultiplier),
            StarBillboardSizing.ScopedCoreCoverage,
            fovMultiplier);

        Assert.Equal(nakedBright, scopedBright, 5);

        var nakedFaint = DrawnCoreScreenShare(
            StarBillboardSizing.CalculateFaintStarAngularScale(scoped: false, 1.0f),
            StarBillboardSizing.NakedEyeFaintCoreCoverage,
            1.0);
        var scopedFaint = DrawnCoreScreenShare(
            StarBillboardSizing.CalculateFaintStarAngularScale(scoped: true, fovMultiplier),
            StarBillboardSizing.ScopedCoreCoverage,
            fovMultiplier);

        Assert.Equal(nakedFaint, scopedFaint, 5);
    }

    [Fact]
    public void A_Scoped_Star_Holds_A_Constant_Screen_Size_As_The_Scope_Winds_In()
    {
        // Screen size goes as angular size over the field of view, so a scale that tracks the field
        // leaves the ratio flat however far the scope is wound in.
        var wide = StarBillboardSizing.CalculateBrightStarAngularScale(scoped: true, 0.45f) / 0.45f;
        var brass = StarBillboardSizing.CalculateBrightStarAngularScale(scoped: true, BrassTelescopeFovMultiplier)
            / BrassTelescopeFovMultiplier;
        var precision = StarBillboardSizing.CalculateBrightStarAngularScale(scoped: true, PrecisionTelescopeFovMultiplier)
            / PrecisionTelescopeFovMultiplier;

        Assert.Equal(wide, brass, 5);
        Assert.Equal(wide, precision, 5);
    }

    /// <summary>
    /// A scoped star is no longer held down to the size of the stars photographed into the deep-sky
    /// plates. It does not need to be: the plates now fade the catalog out where they are drawn,
    /// which is the only place the two were ever in conflict.
    /// </summary>
    [Fact]
    public void A_Scoped_Star_Is_No_Longer_Sized_By_The_Plates()
    {
        var drawnCoreDeg = StarBillboardSizing.MaximumCoreDiameterPixels
            * StarAngularSizePerPixelDeg
            * StarBillboardSizing.CalculateBrightStarAngularScale(scoped: true, BrassTelescopeFovMultiplier)
            * StarBillboardSizing.ScopedCoreCoverage;

        // Still the same order as a plate's own brightest stars rather than a blob across the frame,
        // but no longer squeezed under them.
        Assert.InRange(drawnCoreDeg, WidestPlateStarDeg, WidestPlateStarDeg * 3.0);
    }

    [Fact]
    public void A_Scoped_Planet_Opens_Into_A_Disc_While_The_Stars_Stay_Points()
    {
        foreach (var fovMultiplier in new[] { BrassTelescopeFovMultiplier, PrecisionTelescopeFovMultiplier })
        {
            var star = StarBillboardSizing.CalculateBrightStarAngularScale(scoped: true, fovMultiplier);
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
