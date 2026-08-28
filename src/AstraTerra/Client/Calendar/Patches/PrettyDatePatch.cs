using System.Reflection;
using HarmonyLib;

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

        var hourOfDay = __instance is Vintagestory.API.Common.IGameCalendar calendar ? calendar.HourOfDay : 0f;
        __result = VanillaCalendarHooks.Redact(VanillaCalendarHooks.Display, __result, hourOfDay);
    }
}
