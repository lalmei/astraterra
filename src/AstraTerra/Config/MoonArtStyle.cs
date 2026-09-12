namespace AstraTerra.Config;

/// <summary>
/// Which picture the moon overhead is drawn from: the pixel art, the photographs, or Vintage Story's
/// own disc.
/// </summary>
/// <remarks>
/// The planets keep their own setting. This is only the moon in this world's sky, not the moons of
/// Jupiter. Vanilla — Vintage Story's own disc, left alone — is the default for now; pixel art sits
/// with the game's own look, and photo uses the real lunar surface. Position, phase, moonlight and
/// the length of the night are untouched in every case — only the picture changes, or is left
/// alone.
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

    /// <summary>
    /// What an absent or unreadable setting falls back to. Vanilla for now, so a fresh world keeps
    /// Vintage Story's own moon until the replacement art is ready to lead.
    /// </summary>
    public const MoonArtStyle DefaultStyle = MoonArtStyle.Vanilla;

    public const string DefaultValue = VanillaValue;

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
                style = DefaultStyle;
                return false;
        }
    }

    public static MoonArtStyle ParseOrDefault(string? value)
        => TryParse(value, out var style) ? style : DefaultStyle;

    public static string ToConfigValue(MoonArtStyle style)
        => style switch
        {
            MoonArtStyle.Pixel => PixelValue,
            MoonArtStyle.Photo => PhotoValue,
            _ => VanillaValue
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
