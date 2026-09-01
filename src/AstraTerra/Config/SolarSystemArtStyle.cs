namespace AstraTerra.Config;

/// <summary>
/// Which set of pictures the planets are drawn from: the pixel art, or the photographs.
/// </summary>
/// <remarks>
/// The two sets say the same things about the sky — the same bodies, at the same widths, in the
/// same places — and differ only in how they are drawn. The pixel art is the default because it
/// sits with the game's own art; the photographs stay a command away for anyone who would rather
/// have the real surfaces, and are the only set that draws a planet's phase as its own picture.
/// The moon overhead is a separate setting.
/// </remarks>
public enum SolarSystemArtStyle
{
    Pixel,
    Photo
}

public static class SolarSystemArtStyleParser
{
    public const string PixelValue = "pixel";
    public const string PhotoValue = "photo";

    public static bool TryParse(string? value, out SolarSystemArtStyle style)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case PixelValue:
            case "pixelart":
            case "pixel-art":
                style = SolarSystemArtStyle.Pixel;
                return true;
            case PhotoValue:
            case "photograph":
            case "photographs":
            case "real":
                style = SolarSystemArtStyle.Photo;
                return true;
            default:
                style = SolarSystemArtStyle.Pixel;
                return false;
        }
    }

    public static SolarSystemArtStyle ParseOrDefault(string? value)
        => TryParse(value, out var style) ? style : SolarSystemArtStyle.Pixel;

    public static string ToConfigValue(SolarSystemArtStyle style)
        => style switch
        {
            SolarSystemArtStyle.Photo => PhotoValue,
            _ => PixelValue
        };
}
