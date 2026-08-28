namespace AstraTerra.Config;

/// <summary>
/// How much of Vintage Story's own reckoning of the date stays on screen.
/// </summary>
/// <remarks>
/// The disc measures the length of the year and says when the seasons turn. That is only a tool
/// while the game is not already answering it: with vanilla's calendar in the character panel, a
/// year of scratching bronze is decoration. This is the switch that makes tier 0 pay, and it is off
/// by default because taking the date away is a choice about what kind of game this is.
/// </remarks>
public enum CalendarDisplay
{
    /// <summary>Vintage Story's date and hour, untouched.</summary>
    Full,

    /// <summary>The hour stays — the sun gives it away anyway — and the date goes.</summary>
    Clock,

    /// <summary>Neither. The sky is the only clock and the only calendar.</summary>
    None
}

public static class CalendarDisplayParser
{
    public const string FullValue = "full";
    public const string ClockValue = "clock";
    public const string NoneValue = "none";

    public static bool TryParse(string? value, out CalendarDisplay display)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case FullValue:
            case "on":
            case "vanilla":
                display = CalendarDisplay.Full;
                return true;
            case ClockValue:
            case "hour":
            case "time":
                display = CalendarDisplay.Clock;
                return true;
            case NoneValue:
            case "off":
            case "hidden":
                display = CalendarDisplay.None;
                return true;
            default:
                display = CalendarDisplay.Full;
                return false;
        }
    }

    public static CalendarDisplay ParseOrDefault(string? value)
        => TryParse(value, out var display) ? display : CalendarDisplay.Full;

    public static string ToConfigValue(CalendarDisplay display)
        => display switch
        {
            CalendarDisplay.Clock => ClockValue,
            CalendarDisplay.None => NoneValue,
            _ => FullValue
        };

    /// <summary>Whether the day, month and year are still shown.</summary>
    public static bool ShowsDate(CalendarDisplay display)
        => display is CalendarDisplay.Full;

    /// <summary>Whether the hour of the day is still shown.</summary>
    public static bool ShowsHour(CalendarDisplay display)
        => display is CalendarDisplay.Full or CalendarDisplay.Clock;
}
