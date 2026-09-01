namespace AstraTerra.Config;

/// <summary>
/// Which clock reading the player is shown. The world's internal time never changes.
/// </summary>
public enum DisplayedClockTime
{
    /// <summary>The server's single universal clock.</summary>
    Universal,

    /// <summary>Continuous local apparent solar time at the observer's longitude.</summary>
    LocalSolar,

    /// <summary>Whole-clock-hour zones distributed evenly around the world.</summary>
    LocalTimeZone
}

public static class DisplayedClockTimeParser
{
    public const string UniversalValue = "universal";
    public const string LocalSolarValue = "local";
    public const string LocalTimeZoneValue = "zones";

    public static bool TryParse(string? value, out DisplayedClockTime display)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case UniversalValue:
            case "world":
            case "utc":
                display = DisplayedClockTime.Universal;
                return true;
            case LocalSolarValue:
            case "local-solar":
            case "solar":
                display = DisplayedClockTime.LocalSolar;
                return true;
            case LocalTimeZoneValue:
            case "zone":
            case "time-zone":
            case "time-zones":
                display = DisplayedClockTime.LocalTimeZone;
                return true;
            default:
                display = DisplayedClockTime.LocalSolar;
                return false;
        }
    }

    public static DisplayedClockTime ParseOrDefault(string? value)
        => TryParse(value, out var display) ? display : DisplayedClockTime.LocalSolar;

    public static string ToConfigValue(DisplayedClockTime display)
        => display switch
        {
            DisplayedClockTime.Universal => UniversalValue,
            DisplayedClockTime.LocalTimeZone => LocalTimeZoneValue,
            _ => LocalSolarValue
        };
}
