using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SightingTargetPolicyTests
{
    [Fact]
    public void A_Point_Body_Is_Sighted_Only_Inside_The_Reticle()
    {
        Assert.NotNull(SightingTargetPolicy.Rank(distancePixels: 43.0, discRadiusPixels: 0.0));
        Assert.Null(SightingTargetPolicy.Rank(distancePixels: 45.0, discRadiusPixels: 0.0));
    }

    /// <summary>
    /// The reason any of this exists: a parent giant filling a fifth of the sky is sighted from
    /// anywhere on its face, not only from a reticle's width of its centre.
    /// </summary>
    [Fact]
    public void A_Disc_Is_Sighted_Anywhere_On_It()
    {
        var farOutside = SightingTargetPolicy.Rank(distancePixels: 400.0, discRadiusPixels: 600.0);

        Assert.NotNull(farOutside);
        Assert.Equal(0, farOutside!.Value.Tier);
        Assert.Null(SightingTargetPolicy.Rank(distancePixels: 700.0, discRadiusPixels: 600.0));
    }

    /// <summary>
    /// Discs draw inside the star sphere, so a star behind one cannot be seen and must not be what
    /// the sight reports — even when the star is dead centre and the disc is not.
    /// </summary>
    [Fact]
    public void A_Disc_Under_The_Crosshair_Beats_A_Point_Dead_Centre()
    {
        var disc = SightingTargetPolicy.Rank(distancePixels: 300.0, discRadiusPixels: 600.0)!.Value;
        var star = SightingTargetPolicy.Rank(distancePixels: 0.0, discRadiusPixels: 0.0)!.Value;

        Assert.True(disc.CompareTo(star) < 0);
    }

    [Fact]
    public void The_Nearer_Point_Wins_Among_Points()
    {
        var near = SightingTargetPolicy.Rank(distancePixels: 4.0, discRadiusPixels: 0.0)!.Value;
        var far = SightingTargetPolicy.Rank(distancePixels: 20.0, discRadiusPixels: 0.0)!.Value;

        Assert.True(near.CompareTo(far) < 0);
    }

    /// <summary>A sibling moon crossing the parent's face is what the observer is looking at.</summary>
    [Fact]
    public void The_Smaller_Disc_Wins_Among_Overlapping_Discs()
    {
        var moon = SightingTargetPolicy.Rank(distancePixels: 10.0, discRadiusPixels: 30.0)!.Value;
        var giant = SightingTargetPolicy.Rank(distancePixels: 10.0, discRadiusPixels: 600.0)!.Value;

        Assert.True(moon.CompareTo(giant) < 0);
    }

    [Fact]
    public void Pixels_Per_Degree_Follows_The_Field_Of_View()
    {
        // A 90 degree vertical field on a 1080 pixel frame: focal term 1, half the frame across 45
        // degrees of tangent, so a degree at the centre is 1080/2 * pi/180 pixels.
        var scale = SightingTargetPolicy.PixelsPerDegree(verticalFocalTerm: 1.0, frameHeightPixels: 1080.0);

        Assert.Equal(540.0 * Math.PI / 180.0, scale, 9);

        // Zooming in narrows the field, which is a larger focal term and more pixels per degree.
        Assert.True(SightingTargetPolicy.PixelsPerDegree(4.0, 1080.0) > scale);
    }

    [Theory]
    [InlineData(0.0, 1080.0)]
    [InlineData(-1.0, 1080.0)]
    [InlineData(1.0, 0.0)]
    public void An_Unusable_Projection_Gives_No_Scale(double focalTerm, double frameHeight)
        => Assert.Equal(0.0, SightingTargetPolicy.PixelsPerDegree(focalTerm, frameHeight));

    [Fact]
    public void A_Body_The_Projection_Could_Not_Place_Is_Not_Sighted()
        => Assert.Null(SightingTargetPolicy.Rank(double.NaN, 600.0));
}
