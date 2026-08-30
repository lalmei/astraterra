using AstraTerra.Config;

namespace AstraTerra.Astronomy;

/// <summary>
/// Which picture of a body is actually drawn, once the player's choice of art is applied.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue names one picture per body — the photograph — and everything that places a body
/// on the sky works from that name. This is the last step before the picture is loaded: it swaps
/// the photograph for the pixel-art one drawn of the same body. Nothing else moves. The quad is
/// already built, at a width the catalogue decided, and both sets are cut to that same convention:
/// a square whose width is the body's own image width, so the swap changes the style of the sky
/// and not its scale.
/// </para>
/// <para>
/// The pixel art draws each planet once rather than at three phase angles, so all three of a
/// planet's faces resolve to the one picture; and it draws only the larger moons, so the smaller
/// ones share the small moon it does draw. A path this does not know is handed back unchanged,
/// which is what keeps a body added to the catalogue tomorrow drawable today.
/// </para>
/// </remarks>
public static class SolarSystemTextures
{
    private const string PhotoFolder = "astraterra:environment/solar-system/";
    private const string PixelFolder = "astraterra:environment/solar-system-pixel/";

    /// <summary>The moon the pixel art does not draw a portrait of: Callisto and Saturn's small four.</summary>
    private const string SmallMoon = PixelFolder + "small-moon";

    private static readonly Dictionary<string, string> PixelArt = new(StringComparer.OrdinalIgnoreCase)
    {
        [PhotoFolder + "mercury-0"] = PixelFolder + "mercury",
        [PhotoFolder + "mercury-90"] = PixelFolder + "mercury",
        [PhotoFolder + "mercury-180"] = PixelFolder + "mercury",
        [PhotoFolder + "venus-0"] = PixelFolder + "venus",
        [PhotoFolder + "venus-90"] = PixelFolder + "venus",
        [PhotoFolder + "venus-180"] = PixelFolder + "venus",
        [PhotoFolder + "mars-0"] = PixelFolder + "mars",
        [PhotoFolder + "mars-90"] = PixelFolder + "mars",
        [PhotoFolder + "mars-180"] = PixelFolder + "mars",
        [PhotoFolder + "jupiter-0"] = PixelFolder + "jupiter",
        [PhotoFolder + "jupiter-90"] = PixelFolder + "jupiter",
        [PhotoFolder + "jupiter-180"] = PixelFolder + "jupiter",
        [PhotoFolder + "saturn-0"] = PixelFolder + "saturn",
        [PhotoFolder + "saturn-90"] = PixelFolder + "saturn",
        [PhotoFolder + "saturn-180"] = PixelFolder + "saturn",
        [PhotoFolder + "io"] = PixelFolder + "io",
        [PhotoFolder + "europa"] = PixelFolder + "europa",
        [PhotoFolder + "ganymede"] = PixelFolder + "ganymede",
        [PhotoFolder + "titan"] = PixelFolder + "titan",
        [PhotoFolder + "callisto"] = SmallMoon,
        [PhotoFolder + "enceladus"] = SmallMoon,
        [PhotoFolder + "tethys"] = SmallMoon,
        [PhotoFolder + "dione"] = SmallMoon,
        [PhotoFolder + "rhea"] = SmallMoon,
        [PhotoFolder + "moon-new"] = PixelFolder + "moon-new",
        [PhotoFolder + "moon-waxing-crescent"] = PixelFolder + "moon-waxing-crescent",
        [PhotoFolder + "moon-first-quarter"] = PixelFolder + "moon-first-quarter",
        [PhotoFolder + "moon-waxing-gibbous"] = PixelFolder + "moon-waxing-gibbous",
        [PhotoFolder + "moon-full"] = PixelFolder + "moon-full",
        [PhotoFolder + "moon-waning-gibbous"] = PixelFolder + "moon-waning-gibbous",
        [PhotoFolder + "moon-last-quarter"] = PixelFolder + "moon-last-quarter",
        [PhotoFolder + "moon-waning-crescent"] = PixelFolder + "moon-waning-crescent"
    };

    /// <summary>Every picture the pixel-art set is expected to supply, for the asset tests.</summary>
    public static IReadOnlyCollection<string> PixelArtTexturePaths { get; }
        = PixelArt.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The picture to load for a catalogued body, under the art the player has chosen.</summary>
    public static string Resolve(string texturePath, SolarSystemArtStyle style)
    {
        ArgumentNullException.ThrowIfNull(texturePath);
        if (style != SolarSystemArtStyle.Pixel)
        {
            return texturePath;
        }

        return PixelArt.TryGetValue(texturePath, out var pixelArt) ? pixelArt : texturePath;
    }
}
