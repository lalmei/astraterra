using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyStarSunMoonRendererTests
{
    [Theory]
    [InlineData(0.00, false, false)]
    [InlineData(0.04, false, false)]
    [InlineData(0.10, false, false)]
    [InlineData(0.11, false, true)]
    [InlineData(0.00, true, true)]
    public void ShouldRenderForDarkness_Skips_Near_Daylight_Unless_Forced(double naturalDarkness, bool forceDaylight, bool expected)
    {
        var shouldRender = SkyStarSunMoonRenderer.ShouldRenderForDarkness(naturalDarkness, forceDaylight);

        Assert.Equal(expected, shouldRender);
    }

    /// <summary>
    /// A telescope steadies the air out of the image, so the scoped sky may not reuse the naked-eye
    /// rays. The check that matters is against the deep-sky plates the scoped sky draws catalog stars
    /// on top of: the stars photographed into those plates are round cores with soft halos, and a
    /// rayed sprite beside them reads as a different kind of object.
    /// </summary>
    [Fact]
    public void The_Scoped_Sky_Uses_Its_Own_Sprites()
    {
        string[] nakedEye =
        [
            SkyStarSunMoonRenderer.StarTexturePath,
            SkyStarSunMoonRenderer.FaintStarTexturePath
        ];
        string[] scoped =
        [
            SkyStarSunMoonRenderer.TelescopeStarTexturePath,
            SkyStarSunMoonRenderer.TelescopeFaintStarTexturePath,
            SkyStarSunMoonRenderer.TelescopePlanetTexturePath
        ];

        Assert.Empty(scoped.Intersect(nakedEye, StringComparer.Ordinal));
        Assert.Equal(scoped.Length, scoped.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(scoped, path => path.Contains("rays", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_Sky_Sprite_Exists_In_The_Shipped_Assets()
    {
        string[] paths =
        [
            SkyStarSunMoonRenderer.StarTexturePath,
            SkyStarSunMoonRenderer.FaintStarTexturePath,
            SkyStarSunMoonRenderer.TelescopeStarTexturePath,
            SkyStarSunMoonRenderer.TelescopeFaintStarTexturePath,
            SkyStarSunMoonRenderer.TelescopePlanetTexturePath
        ];

        foreach (var path in paths)
        {
            var file = Path.Combine("assets/astraterra/textures", path["astraterra:".Length..] + ".png");

            Assert.True(File.Exists(file), $"Sky sprite is missing: {file}");
        }
    }
}
