namespace AstraTerra.Config;

/// <summary>
/// Which picture the moon overhead is drawn from: the pixel art, the photographs, or Vintage Story's
/// own disc.
/// </summary>
/// <remarks>
/// The planets keep their own setting. This is only the moon in this world's sky, not the moons of
/// Jupiter. Pixel art is the default because it sits with the game's own; photo uses the real lunar
/// surface; and vanilla is the original disc for anyone who would rather have that back. Position,
/// phase, moonlight and the length of the night are untouched in every case — only the picture
/// changes, or is left alone.
/// </remarks>
public enum MoonArtStyle
{
    Pixel,
    Photo,
    Vanilla
}

public static class MoonArtStyleParser
{
    public const string PixelValue = SolarSystemArtStyleParser.PixelValue;
    public const string PhotoValue = SolarSystemArtStyleParser.PhotoValue;
    public const string VanillaValue = StarfieldModeParser.VanillaValue;

    public static bool TryParse(string? value, out MoonArtStyle style)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case PixelValue:
            case "pixelart":
            case "pixel-art":
                style = MoonArtStyle.Pixel;
                return true;
            case PhotoValue:
            case "photograph":
            case "photographs":
            case "real":
                style = MoonArtStyle.Photo;
                return true;
            case VanillaValue:
            case "original":
            case "vs":
                style = MoonArtStyle.Vanilla;
                return true;
            default:
                style = MoonArtStyle.Pixel;
                return false;
        }
    }

    public static MoonArtStyle ParseOrDefault(string? value)
        => TryParse(value, out var style) ? style : MoonArtStyle.Pixel;

    public static string ToConfigValue(MoonArtStyle style)
        => style switch
        {
            MoonArtStyle.Photo => PhotoValue,
            MoonArtStyle.Vanilla => VanillaValue,
            _ => PixelValue
        };

    /// <summary>
    /// Whether AstraTerra should draw its own moon and ask Vintage Story's to stand down.
    /// </summary>
    public static bool ReplacesVanillaMoon(MoonArtStyle style)
        => style is not MoonArtStyle.Vanilla;

    /// <summary>
    /// The solar-system art folder that holds the moon portrait. Only meaningful while a replacement
    /// moon is being drawn.
    /// </summary>
    public static SolarSystemArtStyle ToTextureStyle(MoonArtStyle style)
        => style == MoonArtStyle.Photo ? SolarSystemArtStyle.Photo : SolarSystemArtStyle.Pixel;
}
