namespace AstraTerra.Client.Rendering;

/// <summary>One independently drawable part of the sky pass.</summary>
[Flags]
public enum SkyRenderPath
{
    None = 0,
    Stars = 1,
    Constellations = 2,
    DeepSky = 4,
    Meteors = 8,
    Comets = 16,
    MilkyWay = 32,
    All = Stars | Constellations | DeepSky | Meteors | Comets | MilkyWay
}

/// <summary>
/// Which parts of the sky are currently being drawn.
/// </summary>
/// <remarks>
/// A diagnostic, not a setting: session-scoped and deliberately not persisted, so a player who
/// switches something off to answer a question gets it back next launch. The point is that
/// "the sky is expensive" can be narrowed to *which path* is expensive in about thirty seconds,
/// without a rebuild — a player reporting a frame-rate drop can answer it themselves.
/// </remarks>
public static class SkyRenderPaths
{
    private static SkyRenderPath enabled = SkyRenderPath.All;

    public static SkyRenderPath Enabled => enabled;

    public static bool IsEnabled(SkyRenderPath path) => (enabled & path) == path;

    public static void Set(SkyRenderPath path, bool on)
    {
        enabled = on ? enabled | path : enabled & ~path;
    }

    public static void Reset() => enabled = SkyRenderPath.All;

    /// <summary>Every path and whether it is drawing, for the command's report line.</summary>
    public static string Describe()
        => string.Join(
            "; ",
            SkyRenderPathParser.Names.Select(name =>
                $"{name}={(IsEnabled(SkyRenderPathParser.ParseOrThrow(name)) ? "on" : "off")}"));
}

public static class SkyRenderPathParser
{
    public static readonly string[] Names = ["stars", "constellations", "deepsky", "meteors", "comets", "milkyway"];

    public static bool TryParse(string? value, out SkyRenderPath path)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "stars":
            case "star":
                path = SkyRenderPath.Stars;
                return true;
            case "constellations":
            case "constellation":
            case "lines":
                path = SkyRenderPath.Constellations;
                return true;
            case "deepsky":
            case "deep-sky":
                path = SkyRenderPath.DeepSky;
                return true;
            case "meteors":
            case "meteor":
                path = SkyRenderPath.Meteors;
                return true;
            case "comets":
            case "comet":
                path = SkyRenderPath.Comets;
                return true;
            case "milkyway":
            case "milky-way":
            case "galaxy":
                path = SkyRenderPath.MilkyWay;
                return true;
            case "all":
                path = SkyRenderPath.All;
                return true;
            default:
                path = SkyRenderPath.None;
                return false;
        }
    }

    internal static SkyRenderPath ParseOrThrow(string value)
        => TryParse(value, out var path) ? path : throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown sky render path.");
}
