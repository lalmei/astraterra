using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class StarRenderModelTests
{
    [Fact]
    public void ProjectVisibleStars_Drops_Stars_Below_Visual_Horizon_Band()
    {
        var stars = new[]
        {
            new StarCatalogEntry(1, 0, 0, 1, null, false),
            new StarCatalogEntry(2, 180, 0, 1, null, false)
        };

        var visible = StarRenderModel.ProjectVisibleStars(stars, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Single(visible);
        Assert.Equal(1, visible.Single().Hip);
    }

    [Fact]
    public void Project_Keeps_Stars_Slightly_Below_Mathematical_Horizon()
    {
        var star = new StarCatalogEntry(1, 100, 0, 1, null, false);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.NotNull(rendered);
        Assert.InRange(rendered!.AltitudeDeg, -11.0, -9.0);
        Assert.True(rendered.Brightness > 0);
    }

    [Fact]
    public void Project_Drops_Stars_Below_Extended_Visual_Horizon()
    {
        var star = new StarCatalogEntry(1, 120, 0, 1, null, false);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Null(rendered);
    }

    [Fact]
    public void Project_Dims_Stars_Near_Horizon()
    {
        var highStar = new StarCatalogEntry(1, 0, 0, 1, null, false);
        var lowStar = new StarCatalogEntry(2, 84, 0, 1, null, false);

        var high = StarRenderModel.Project(highStar, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1)!;
        var low = StarRenderModel.Project(lowStar, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1)!;

        Assert.True(high.Brightness > low.Brightness);
    }

    [Fact]
    public void Project_Preserves_Guide_Star_Flag()
    {
        var star = new StarCatalogEntry(677, 0, 0, 2.07, 0.15, true);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(rendered!.IsGuideStar);
    }

    [Fact]
    public void Project_Preserves_Visual_Magnitude_For_Texture_Selection()
    {
        var star = new StarCatalogEntry(677, 0, 0, 5.25, 0.15, false);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Equal(5.25, rendered!.VisualMagnitude);
    }

    [Fact]
    public void Project_Produces_Normalized_Unit_Sphere_Direction()
    {
        var star = new StarCatalogEntry(677, 0, 0, 2.07, 0.15, true);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        var length = Math.Sqrt(
            (rendered!.DirectionX * rendered.DirectionX) +
            (rendered.DirectionY * rendered.DirectionY) +
            (rendered.DirectionZ * rendered.DirectionZ));
        Assert.Equal(1.0, length, precision: 6);
    }

    [Fact]
    public void Project_Maps_Northern_Sky_To_Negative_Z()
    {
        var polarisLikeStar = new StarCatalogEntry(11767, 0, 89.0, 2.0, 0.6, true);

        var rendered = StarRenderModel.Project(polarisLikeStar, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(rendered!.DirectionZ < 0);
        Assert.InRange(rendered.AzimuthDeg, 0.0, 1.0);
    }

    [Fact]
    public void Project_Clamps_Brightness_And_Produces_Positive_Size()
    {
        var star = new StarCatalogEntry(677, 0, 0, -2, 0.15, true);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 100);

        Assert.InRange(rendered!.Brightness, 0.0, 1.0);
        Assert.True(rendered.Size > 0);
    }

    [Fact]
    public void Project_Makes_Naked_Eye_Limit_Stars_Subtle_At_Default_Bias()
    {
        var star = new StarCatalogEntry(677, 0, 0, 6.0, 0.15, false);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.InRange(rendered!.Brightness, 0.35, 0.37);
    }

    [Fact]
    public void Project_Uses_Stylized_Magnitude_Visibility_For_Dim_Stars()
    {
        var magnitudeTwo = new StarCatalogEntry(677, 0, 0, 2.0, 0.15, false);
        var magnitudeSix = new StarCatalogEntry(678, 0, 0, 6.0, 0.15, false);

        var brighter = StarRenderModel.Project(magnitudeTwo, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);
        var dimmer = StarRenderModel.Project(magnitudeSix, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(brighter!.Brightness > dimmer!.Brightness * 2.2);
        Assert.InRange(dimmer.Brightness, 0.35, 0.37);
    }

    [Fact]
    public void Project_Sizes_Brighter_Stars_Larger_Than_Faint_Stars()
    {
        var bright = new StarCatalogEntry(677, 0, 0, -1.0, 0.15, false);
        var middle = new StarCatalogEntry(678, 0, 0, 2.5, 0.15, false);
        var faint = new StarCatalogEntry(679, 0, 0, 6.0, 0.15, false);

        var brightRendered = StarRenderModel.Project(bright, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);
        var middleRendered = StarRenderModel.Project(middle, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);
        var faintRendered = StarRenderModel.Project(faint, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(brightRendered!.Size > middleRendered!.Size);
        Assert.True(middleRendered.Size > faintRendered!.Size);
        Assert.True(brightRendered.Size > faintRendered.Size * 1.45);
    }

    [Fact]
    public void Project_Gives_Polaris_A_Larger_Bright_Star_Footprint()
    {
        var polaris = new StarCatalogEntry(11767, 37.9546, 89.2641, 1.98, 0.60, true);

        var rendered = StarRenderModel.Project(polaris, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);

        Assert.NotNull(rendered);
        Assert.InRange(rendered!.Size, 16.6, 16.9);
    }

    [Fact]
    public void Project_Keeps_Magnitude_Size_Stable_While_Twilight_Dims_Opacity()
    {
        var polaris = new StarCatalogEntry(11767, 37.9546, 89.2641, 1.98, 0.60, true);

        var night = StarRenderModel.Project(polaris, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);
        var twilight = StarRenderModel.Project(polaris, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 0.3);

        Assert.NotNull(night);
        Assert.NotNull(twilight);
        Assert.Equal(night!.Size, twilight!.Size);
        Assert.True(night.Brightness > twilight.Brightness);
    }

    [Fact]
    public void ProjectVisibleStars_Sorts_Brighter_Stars_First_For_Draw_Order()
    {
        var stars = new[]
        {
            new StarCatalogEntry(1, 0, 0, 4, null, false),
            new StarCatalogEntry(2, 0, 0, 1, null, false),
            new StarCatalogEntry(3, 0, 0, 0, null, false)
        };

        var visible = StarRenderModel.ProjectVisibleStars(stars, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Equal(new[] { 3, 2, 1 }, visible.Select(star => star.Hip));
    }
}
