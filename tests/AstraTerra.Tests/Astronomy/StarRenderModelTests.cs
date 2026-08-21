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
        Assert.InRange(rendered!.Value.AltitudeDeg, -11.0, -9.0);
        Assert.True(rendered.Value.Brightness > 0);
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

        var high = StarRenderModel.Project(highStar, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1)!.Value;
        var low = StarRenderModel.Project(lowStar, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1)!.Value;

        Assert.True(high.Brightness > low.Brightness);
    }

    [Fact]
    public void Project_Preserves_Guide_Star_Flag()
    {
        var star = new StarCatalogEntry(677, 0, 0, 2.07, 0.15, true);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(rendered!.Value.IsGuideStar);
    }

    [Fact]
    public void Project_Preserves_Visual_Magnitude_For_Texture_Selection()
    {
        var star = new StarCatalogEntry(677, 0, 0, 5.25, 0.15, false);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Equal(5.25, rendered!.Value.VisualMagnitude);
    }

    [Fact]
    public void Project_Produces_Normalized_Unit_Sphere_Direction()
    {
        var star = new StarCatalogEntry(677, 0, 0, 2.07, 0.15, true);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        var length = Math.Sqrt(
            (rendered!.Value.DirectionX * rendered.Value.DirectionX) +
            (rendered.Value.DirectionY * rendered.Value.DirectionY) +
            (rendered.Value.DirectionZ * rendered.Value.DirectionZ));
        Assert.Equal(1.0, length, precision: 6);
    }

    [Fact]
    public void Project_Maps_Northern_Sky_To_Negative_Z()
    {
        var polarisLikeStar = new StarCatalogEntry(11767, 0, 89.0, 2.0, 0.6, true);

        var rendered = StarRenderModel.Project(polarisLikeStar, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(rendered!.Value.DirectionZ < 0);
        Assert.InRange(rendered.Value.AzimuthDeg, 0.0, 1.0);
    }

    [Fact]
    public void Project_Clamps_Brightness_And_Produces_Positive_Size()
    {
        var star = new StarCatalogEntry(677, 0, 0, -2, 0.15, true);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 100);

        Assert.InRange(rendered!.Value.Brightness, 0.0, 1.0);
        Assert.True(rendered.Value.Size > 0);
    }

    [Fact]
    public void Project_Makes_Naked_Eye_Limit_Stars_Subtle_At_Default_Bias()
    {
        var star = new StarCatalogEntry(677, 0, 0, 6.0, 0.15, false);

        var rendered = StarRenderModel.Project(star, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.InRange(rendered!.Value.Brightness, 0.35, 0.37);
    }

    [Fact]
    public void Project_Uses_Stylized_Magnitude_Visibility_For_Dim_Stars()
    {
        var magnitudeTwo = new StarCatalogEntry(677, 0, 0, 2.0, 0.15, false);
        var magnitudeSix = new StarCatalogEntry(678, 0, 0, 6.0, 0.15, false);

        var brighter = StarRenderModel.Project(magnitudeTwo, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);
        var dimmer = StarRenderModel.Project(magnitudeSix, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(brighter!.Value.Brightness > dimmer!.Value.Brightness * 2.2);
        Assert.InRange(dimmer.Value.Brightness, 0.35, 0.37);
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

        Assert.True(brightRendered!.Value.Size > middleRendered!.Value.Size);
        Assert.True(middleRendered.Value.Size > faintRendered!.Value.Size);
        Assert.True(brightRendered.Value.Size > faintRendered.Value.Size * 1.45);
    }

    [Fact]
    public void Project_Gives_Polaris_A_Larger_Bright_Star_Footprint()
    {
        var polaris = new StarCatalogEntry(11767, 37.9546, 89.2641, 1.98, 0.60, true);

        var rendered = StarRenderModel.Project(polaris, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);

        Assert.NotNull(rendered);
        Assert.InRange(rendered!.Value.Size, 16.6, 16.9);
    }

    [Fact]
    public void Project_Keeps_Magnitude_Size_Stable_While_Twilight_Dims_Opacity()
    {
        var polaris = new StarCatalogEntry(11767, 37.9546, 89.2641, 1.98, 0.60, true);

        var night = StarRenderModel.Project(polaris, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);
        var twilight = StarRenderModel.Project(polaris, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 0.3);

        Assert.NotNull(night);
        Assert.NotNull(twilight);
        Assert.Equal(night!.Value.Size, twilight!.Value.Size);
        Assert.True(night.Value.Brightness > twilight.Value.Brightness);
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

    /// <summary>
    /// The sky pass projects the whole catalog on the render thread every frame it draws. The LINQ
    /// chain this replaced allocated about 440 KiB a frame -- roughly a gigabyte a minute of garbage
    /// at sixty frames a second, which players saw as stutter, lag spikes and a client that kept the
    /// memory. Pinned here because nothing else fails when it comes back.
    /// </summary>
    [Fact]
    public void ProjectVisibleStars_Into_A_Reused_Buffer_Allocates_Nothing_Per_Frame()
    {
        var stars = BuildCatalog(5000);
        var buffer = new List<RenderedStar>(6000);
        StarRenderModel.ProjectVisibleStars(stars, 45, 100, 1, buffer);
        Assert.NotEmpty(buffer);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 30; frame++)
        {
            StarRenderModel.ProjectVisibleStars(stars, 45, 100 + (frame * 0.004), 1, buffer);
        }

        var allocatedPerFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / 30;
        Assert.True(allocatedPerFrame < 1024, $"Projecting into a reused buffer allocated {allocatedPerFrame} bytes per frame.");
    }

    [Fact]
    public void Projecting_Into_A_Buffer_Gives_The_Same_Sky_As_Building_A_List()
    {
        var stars = BuildCatalog(500);
        var buffer = new List<RenderedStar>();

        var built = StarRenderModel.ProjectVisibleStars(stars, latitudeDeg: 52, localSiderealDeg: 217, brightnessBias: 1);
        StarRenderModel.ProjectVisibleStars(stars, 52, 217, 1, buffer);

        Assert.Equal(built, buffer);
    }

    /// <summary>A spread of positions and magnitudes, so roughly half the sky is above the horizon.</summary>
    private static IReadOnlyList<StarCatalogEntry> BuildCatalog(int count)
    {
        var stars = new List<StarCatalogEntry>(count);
        for (var index = 0; index < count; index++)
        {
            stars.Add(new StarCatalogEntry(
                index,
                index * 360.0 / count,
                ((index * 7919 % 180) - 90) * 0.98,
                (index % 60) / 10.0,
                ((index % 20) / 10.0) - 0.4,
                index % 50 == 0));
        }

        return stars;
    }
}
