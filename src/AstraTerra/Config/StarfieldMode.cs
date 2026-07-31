namespace AstraTerra.Config;

public enum StarfieldMode
{
    AstraTerra,
    Both,
    Vanilla
}

public static class StarfieldModeParser
{
    public const string AstraTerraValue = "astraterra";
    public const string BothValue = "both";
    public const string VanillaValue = "vanilla";

    public static bool TryParse(string? value, out StarfieldMode mode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case AstraTerraValue:
            case "astra":
                mode = StarfieldMode.AstraTerra;
                return true;
            case BothValue:
                mode = StarfieldMode.Both;
                return true;
            case VanillaValue:
                mode = StarfieldMode.Vanilla;
                return true;
            default:
                mode = StarfieldMode.AstraTerra;
                return false;
        }
    }

    public static StarfieldMode ParseOrDefault(string? value)
        => TryParse(value, out var mode) ? mode : StarfieldMode.AstraTerra;

    public static string ToConfigValue(StarfieldMode mode)
        => mode switch
        {
            StarfieldMode.Both => BothValue,
            StarfieldMode.Vanilla => VanillaValue,
            _ => AstraTerraValue
        };

    public static bool ShowsAstraTerra(StarfieldMode mode)
        => mode is StarfieldMode.AstraTerra or StarfieldMode.Both;

    public static bool ShowsVanilla(StarfieldMode mode)
        => mode is StarfieldMode.Vanilla or StarfieldMode.Both;
}
