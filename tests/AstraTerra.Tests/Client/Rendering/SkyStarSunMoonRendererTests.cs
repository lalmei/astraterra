using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyStarSunMoonRendererTests
{
    /// <summary>
    /// A mod that authors the starfield from the world seed can only supply it after the server has
    /// said which world this is, which is long after the shipped catalog loads.
    /// </summary>
    [Fact]
    public void ReplaceCatalog_Swaps_The_Stars_The_Sky_Draws_From()
    {
        SkyStarSunMoonRenderer.Reset();
        Assert.Equal(0, SkyStarSunMoonRenderer.CatalogStarCount);

        SkyStarSunMoonRenderer.ReplaceCatalog(CatalogOf(3));
        Assert.Equal(3, SkyStarSunMoonRenderer.CatalogStarCount);

        SkyStarSunMoonRenderer.ReplaceCatalog(CatalogOf(7));
        Assert.Equal(7, SkyStarSunMoonRenderer.CatalogStarCount);

        SkyStarSunMoonRenderer.Reset();
        Assert.Equal(0, SkyStarSunMoonRenderer.CatalogStarCount);
    }

    [Fact]
    public void ReplaceCatalog_Rejects_A_Missing_Catalog()
        => Assert.Throws<ArgumentNullException>(() => SkyStarSunMoonRenderer.ReplaceCatalog(null!));

    /// <summary>
    /// The shipped planets, comets and showers all belong to this solar system, so a mod that moves
    /// the world elsewhere needs to be able to say the sky has none of them.
    /// </summary>
    [Fact]
    public void The_Solar_System_Can_Be_Emptied_For_A_Sky_That_Has_None()
    {
        SkyStarSunMoonRenderer.Reset();
        SkyStarSunMoonRenderer.ReplaceMeteorShowers([
            new MeteorShowerEntry("test", "Test", 45.0, 20.0, 120.0, 5.0, 40.0)
        ]);
        Assert.Equal(1, SkyStarSunMoonRenderer.CatalogMeteorShowerCount);

        SkyStarSunMoonRenderer.ReplacePlanetCatalog(null);
        SkyStarSunMoonRenderer.ReplaceCometCatalog(null);
        SkyStarSunMoonRenderer.ReplaceMeteorShowers([]);

        Assert.Equal(0, SkyStarSunMoonRenderer.CatalogPlanetCount);
        Assert.Equal(0, SkyStarSunMoonRenderer.CatalogCometCount);
        Assert.Equal(0, SkyStarSunMoonRenderer.CatalogMeteorShowerCount);
        SkyStarSunMoonRenderer.Reset();
    }

    [Fact]
    public void ReplaceMeteorShowers_Rejects_A_Missing_List()
        => Assert.Throws<ArgumentNullException>(() => SkyStarSunMoonRenderer.ReplaceMeteorShowers(null!));

    private static StarCatalog CatalogOf(int stars)
        => new(
            Enumerable.Range(1, stars)
                .Select(hip => new StarCatalogEntry(hip, hip * 10.0, 0.0, 2.0, 0.5, false))
                .ToList(),
            []);

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
