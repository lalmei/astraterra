namespace AstraTerra.Config;

public enum SkyGridMode
{
    None,
    Horizontal,
    Equatorial,
    Both
}

public static class SkyGridModeParser
{
    public const string NoneValue = "none";
    public const string HorizontalValue = "horizontal";
    public const string EquatorialValue = "equatorial";
    public const string BothValue = "both";

    public static bool TryParse(string? value, out SkyGridMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case NoneValue:
            case "off":
                mode = SkyGridMode.None;
                return true;
            case HorizontalValue:
            case "altaz":
            case "alt-az":
                mode = SkyGridMode.Horizontal;
                return true;
            case EquatorialValue:
            case "radec":
            case "ra-dec":
                mode = SkyGridMode.Equatorial;
                return true;
            case BothValue:
                mode = SkyGridMode.Both;
                return true;
            default:
                mode = SkyGridMode.None;
                return false;
        }
    }

    public static SkyGridMode ParseOrDefault(string? value)
        => TryParse(value, out var mode) ? mode : SkyGridMode.None;

    public static string ToConfigValue(SkyGridMode mode)
        => mode switch
        {
            SkyGridMode.Horizontal => HorizontalValue,
            SkyGridMode.Equatorial => EquatorialValue,
            SkyGridMode.Both => BothValue,
            _ => NoneValue
        };

    public static bool ShowsHorizontal(SkyGridMode mode)
        => mode is SkyGridMode.Horizontal or SkyGridMode.Both;

    public static bool ShowsEquatorial(SkyGridMode mode)
        => mode is SkyGridMode.Equatorial or SkyGridMode.Both;
}
