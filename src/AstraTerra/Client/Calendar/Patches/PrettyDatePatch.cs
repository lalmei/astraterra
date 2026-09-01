using System.Reflection;
using AstraTerra.Client.Calendar;
using AstraTerra.Config;
using HarmonyLib;
using Vintagestory.API.Config;

namespace AstraTerra.Client.Calendar.Patches;

/// <summary>
/// Keeps Vintage Story's own date off the screen when the player has asked for that.
/// </summary>
/// <remarks>
/// <c>GameCalendar.PrettyDate</c> is the single place the game formats the date for a person to
/// read: the character panel's Environment box, and the client's own time report. Nothing decides
/// anything on its result, so redacting it removes the display and leaves the world's clock, the
/// seasons and every other mod untouched.
/// </remarks>
[HarmonyPatch]
public static class PrettyDatePatch
{
    [HarmonyPrepare]
    public static bool Prepare()
        => AccessTools.TypeByName("Vintagestory.Common.GameCalendar") is not null;

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => AccessTools.Method("Vintagestory.Common.GameCalendar:PrettyDate");

    [HarmonyPostfix]
    public static void Postfix(object __instance, ref string __result)
    {
        if (__result is null || !VanillaCalendarHooks.IsPlayerCalendar(__instance))
        {
            return;
        }

        var hourOfDay = __instance is Vintagestory.API.Common.IGameCalendar calendar
            ? VanillaCalendarHooks.GetDisplayedHourOfDay(calendar)
            : 0f;
        if (CalendarDisplayParser.ShowsDate(VanillaCalendarHooks.Display)
            && VanillaCalendarHooks.ClockTime != DisplayedClockTime.Universal
            && TryFormatDate(__instance, hourOfDay, out var displayedDate))
        {
            __result = displayedDate;
        }

        __result = VanillaCalendarHooks.Redact(VanillaCalendarHooks.Display, __result, hourOfDay);
    }

    private static bool TryFormatDate(object calendar, float displayedHour, out string formatted)
    {
        var type = calendar.GetType();
        var day = AccessTools.Property(type, "DayOfMonth")?.GetValue(calendar);
        var monthName = AccessTools.Property(type, "MonthName")?.GetValue(calendar);
        var year = AccessTools.Property(type, "Year")?.GetValue(calendar);
        if (day is null || monthName is null || year is null)
        {
            formatted = string.Empty;
            return false;
        }

        var (hour, minute) = VanillaCalendarHooks.GetDisplayedClockParts(displayedHour);
        formatted = Lang.Get(
            "dateformat",
            day,
            Lang.Get("month-" + monthName),
            Convert.ToDouble(year).ToString("0"),
            hour.ToString("00"),
            minute.ToString("00"));
        return true;
    }
}
