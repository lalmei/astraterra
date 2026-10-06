using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyTextureLoaderTests
{
    /// <summary>
    /// Configured paths use the game's shorthand, so reading the asset directly has to put back what
    /// the shorthand leaves out.
    /// </summary>
    [Theory]
    [InlineData("astraterra:environment/milky-way", "astraterra:textures/environment/milky-way.png")]
    [InlineData("astraterra:environment/deep-sky/stellarium/m31", "astraterra:textures/environment/deep-sky/stellarium/m31.png")]
    [InlineData("astraterra:textures/environment/milky-way.png", "astraterra:textures/environment/milky-way.png")]
    [InlineData("astraterra:Textures/environment/milky-way.PNG", "astraterra:Textures/environment/milky-way.PNG")]
    [InlineData("environment/milky-way", "textures/environment/milky-way.png")]
    public void ToAssetPath_Fills_In_The_Folder_And_Extension_The_Shorthand_Leaves_Out(string configured, string expected)
        => Assert.Equal(expected, SkyTextureLoader.ToAssetPath(configured));

    /// <summary>
    /// A photograph is far smaller as a JPEG, and Skia decodes one fine, so an explicit extension is
    /// kept rather than buried under a second <c>.png</c>.
    /// </summary>
    [Theory]
    [InlineData("astraterra:environment/sky-tiles/l000-b00.jpg")]
    [InlineData("astraterra:environment/sky-tiles/l000-b00.jpeg")]
    [InlineData("astraterra:environment/sky-tiles/l000-b00.webp")]
    public void ToAssetPath_Keeps_An_Image_Extension_That_Is_Already_There(string configured)
        => Assert.EndsWith(Path.GetExtension(configured), SkyTextureLoader.ToAssetPath(configured), StringComparison.Ordinal);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ToAssetPath_Rejects_An_Empty_Path(string configured)
        => Assert.ThrowsAny<ArgumentException>(() => SkyTextureLoader.ToAssetPath(configured));

    /// <summary>
    /// The engine's clamp modes are bare integers. The galactic map has to repeat so smoothing
    /// crosses the longitude seam; a plate has to clamp so its border does not pick up its far edge.
    /// </summary>
    [Fact]
    public void ToClampMode_Maps_Onto_The_Engine_Clamp_Modes()
    {
        Assert.Equal(2, SkyTextureLoader.ToClampMode(SkyTextureWrap.Repeat));
        Assert.Equal(1, SkyTextureLoader.ToClampMode(SkyTextureWrap.ClampToEdge));
    }

    [Fact]
    public void ToClampMode_Rejects_An_Unknown_Wrap()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SkyTextureLoader.ToClampMode((SkyTextureWrap)99));
}
