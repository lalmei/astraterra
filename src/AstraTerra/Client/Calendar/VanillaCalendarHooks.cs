using AstraTerra.Config;
using Vintagestory.API.Client;

namespace AstraTerra.Client.Calendar;

/// <summary>
/// What the game is allowed to tell the player about the date, and how it says so when it will not.
/// </summary>
/// <remarks>
/// Vintage Story writes the day, the month, the year and the hour into the character panel's
/// Environment box. Everything the sky disc measures over a world year is therefore already on
/// screen, for free, from the first night — which makes the disc flavour rather than a tool. Turning
/// this down is the external dependency the disc has: it is the difference between a bronze rim that
/// tells you when to sow and a bronze rim that agrees with a number you could already read.
/// <para>
/// Only the client's own calendar is redacted. A server's answer to <c>/time</c> is an
/// administrator asking the world a direct question, and stays true.
/// </para>
/// </remarks>
public static class VanillaCalendarHooks
{
    private static AstraTerraConfig? config;
    private static ICoreClientAPI? api;

    public static void Initialize(ICoreClientAPI clientApi, AstraTerraConfig clientConfig)
    {
        api = clientApi;
        config = clientConfig;
    }

    public static void Reset()
    {
        api = null;
        config = null;
    }

    /// <summary>
    /// Whether this calendar object is the one the player's own client reads the date off.
    /// </summary>
    public static bool IsPlayerCalendar(object? calendar)
        => calendar is not null && api is not null && ReferenceEquals(calendar, api.World?.Calendar);

    public static CalendarDisplay Display => config?.GetCalendarDisplay() ?? CalendarDisplay.Full;

    /// <summary>
    /// What the date line says instead. It never reports a wrong date and never looks like a
    /// failure: it says the reckoning is not being kept, which is exactly what has happened.
    /// </summary>
    public static string Redact(CalendarDisplay display, string prettyDate, float hourOfDay)
    {
        if (CalendarDisplayParser.ShowsDate(display))
        {
            return prettyDate;
        }

        if (!CalendarDisplayParser.ShowsHour(display))
        {
            return "unreckoned";
        }

        var hour = (int)hourOfDay;
        var minute = (int)((hourOfDay - hour) * 60f);
        return $"unreckoned, {hour:00}:{minute:00}";
    }
}
