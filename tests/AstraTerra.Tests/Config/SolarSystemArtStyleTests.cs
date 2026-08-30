using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class SolarSystemArtStyleTests
{
    [Theory]
    [InlineData("pixel", SolarSystemArtStyle.Pixel)]
    [InlineData(" PIXEL-ART ", SolarSystemArtStyle.Pixel)]
    [InlineData("photo", SolarSystemArtStyle.Photo)]
    [InlineData("Photographs", SolarSystemArtStyle.Photo)]
    public void TryParse_Accepts_Supported_Config_Values(string value, SolarSystemArtStyle expected)
    {
        var parsed = SolarSystemArtStyleParser.TryParse(value, out var style);

        Assert.True(parsed);
        Assert.Equal(expected, style);
    }

    [Fact]
    public void ParseOrDefault_Uses_The_Pixel_Art_For_Unknown_Values()
    {
        Assert.Equal(SolarSystemArtStyle.Pixel, SolarSystemArtStyleParser.ParseOrDefault("unknown"));
    }

    [Theory]
    [InlineData(SolarSystemArtStyle.Pixel, "pixel")]
    [InlineData(SolarSystemArtStyle.Photo, "photo")]
    public void ToConfigValue_Round_Trips(SolarSystemArtStyle style, string expected)
    {
        Assert.Equal(expected, SolarSystemArtStyleParser.ToConfigValue(style));
        Assert.Equal(style, SolarSystemArtStyleParser.ParseOrDefault(expected));
    }

    /// <summary>A fresh config draws the pixel art, which is what a new world shows.</summary>
    [Fact]
    public void A_New_Config_Draws_The_Pixel_Art()
    {
        Assert.Equal(SolarSystemArtStyle.Pixel, new AstraTerraConfig().GetSolarSystemArtStyle());
    }
}
