using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class MoonArtStyleTests
{
    [Theory]
    [InlineData("pixel", MoonArtStyle.Pixel)]
    [InlineData(" PIXEL-ART ", MoonArtStyle.Pixel)]
    [InlineData("photo", MoonArtStyle.Photo)]
    [InlineData("Photographs", MoonArtStyle.Photo)]
    [InlineData("vanilla", MoonArtStyle.Vanilla)]
    [InlineData(" original ", MoonArtStyle.Vanilla)]
    [InlineData("VS", MoonArtStyle.Vanilla)]
    public void TryParse_Accepts_Supported_Config_Values(string value, MoonArtStyle expected)
    {
        var parsed = MoonArtStyleParser.TryParse(value, out var style);

        Assert.True(parsed);
        Assert.Equal(expected, style);
    }

    [Fact]
    public void ParseOrDefault_Uses_The_Pixel_Art_For_Unknown_Values()
    {
        Assert.False(MoonArtStyleParser.TryParse("unknown", out _));
        Assert.Equal(MoonArtStyle.Pixel, MoonArtStyleParser.ParseOrDefault("unknown"));
    }

    [Theory]
    [InlineData(MoonArtStyle.Pixel, "pixel")]
    [InlineData(MoonArtStyle.Photo, "photo")]
    [InlineData(MoonArtStyle.Vanilla, "vanilla")]
    public void ToConfigValue_Round_Trips(MoonArtStyle style, string expected)
    {
        Assert.Equal(expected, MoonArtStyleParser.ToConfigValue(style));
        Assert.Equal(style, MoonArtStyleParser.ParseOrDefault(expected));
    }

    /// <summary>A fresh config draws the pixel moon, which is what a new world shows.</summary>
    [Fact]
    public void A_New_Config_Draws_The_Pixel_Moon()
    {
        Assert.Equal(MoonArtStyle.Pixel, new AstraTerraConfig().GetMoonArtStyle());
    }

    /// <summary>
    /// The planets and the moon are separate choices: setting one must not drag the other along.
    /// </summary>
    [Fact]
    public void The_Moon_Does_Not_Follow_The_Planets()
    {
        var config = new AstraTerraConfig
        {
            SolarSystemArt = SolarSystemArtStyleParser.PhotoValue,
            MoonArt = MoonArtStyleParser.VanillaValue
        };

        Assert.Equal(SolarSystemArtStyle.Photo, config.GetSolarSystemArtStyle());
        Assert.Equal(MoonArtStyle.Vanilla, config.GetMoonArtStyle());
    }

    [Theory]
    [InlineData(MoonArtStyle.Pixel, true, SolarSystemArtStyle.Pixel)]
    [InlineData(MoonArtStyle.Photo, true, SolarSystemArtStyle.Photo)]
    [InlineData(MoonArtStyle.Vanilla, false, SolarSystemArtStyle.Pixel)]
    public void Replacement_Policy_Selects_The_Requested_Moon(
        MoonArtStyle style,
        bool expectedReplacement,
        SolarSystemArtStyle expectedTextures)
    {
        Assert.Equal(expectedReplacement, MoonArtStyleParser.ReplacesVanillaMoon(style));
        Assert.Equal(expectedTextures, MoonArtStyleParser.ToTextureStyle(style));
    }
}
