using HarmonyLib;
using Vintagestory.API.Common;

namespace AstraTerra.Astronomy.Patches;

/// <summary>
/// Puts the parent giant into the light the world actually runs on.
/// </summary>
/// <remarks>
/// <para>
/// Everything in Vintage Story that cares how bright it is outside goes through one method.
/// <c>GetLightLevel</c> resolves <c>MaxTimeOfDayLight</c>, <c>TimeOfDaySunLight</c> and
/// <c>Sunbrightness</c> by taking the sunlight stored in the chunk and multiplying it by
/// <c>IGameCalendar.GetDayLightStrength</c>; mob spawn conditions, crop growth, body temperature
/// and the renderer all read the result. So this is one postfix, not a lighting engine, and it is
/// the narrowest place a second source of daylight can be introduced.
/// </para>
/// <para>
/// The client keeps a second copy of the same number. <c>ClientGameCalendar.Update</c> works out
/// <c>DayLightStrength</c> once a frame for rendering rather than calling the method above, so
/// patching only the method would light the gameplay and leave the sky alone -- a landscape that
/// darkens under an eclipse beneath a sky that does not. Both are patched, from the same
/// controller, so they cannot drift apart.
/// </para>
/// <para>
/// Vanilla already does both of these things for its own moon: <c>GetDayLightStrength</c> carries a
/// moonlight term and an eclipse term that dims daylight when the moon crosses the sun. This is
/// that model given a body forty times wider, which is what a locked moon's sky actually has.
/// </para>
/// <para>
/// Patched by hand rather than by attribute, for the reason the pit kiln patch is: this has to
/// apply on both sides, and an assembly-wide <c>PatchAll</c> on the client would claim it twice.
/// </para>
/// </remarks>
public sealed class NearBodyLightPatchHost
{
    private const string CalendarTypeName = "Vintagestory.Common.GameCalendar";
    private const string ClientCalendarTypeName = "Vintagestory.Common.ClientGameCalendar";

    /// <summary>
    /// The controller every patched call reads. Static because Harmony patches are static methods
    /// and there is exactly one world per side to answer for.
    /// </summary>
    private static NearBodyLightController? controller;

    private readonly Harmony harmony = new("astraterra.nearbodylight");
    private bool applied;

    /// <summary>Whether the game still has the methods this patch is about.</summary>
    public static bool IsAvailable => DayLightStrengthMethod is not null;

    /// <summary>
    /// The one funnel: every time-of-day light query in the game resolves through this.
    /// </summary>
    /// <remarks>
    /// Looked up by name rather than off a type, because <c>GameCalendar</c> lives in
    /// <c>VintagestoryLib</c> behind the <see cref="IGameCalendar"/> the mod compiles against, and
    /// the interface does not carry the two-coordinate overload that the block-light path calls.
    /// </remarks>
    private static System.Reflection.MethodInfo? DayLightStrengthMethod
        => AccessTools.Method(
            AccessTools.TypeByName(CalendarTypeName),
            "GetDayLightStrength",
            [typeof(double), typeof(double)]);

    /// <summary>The client's own per-frame copy of the same number, used for rendering.</summary>
    private static System.Reflection.MethodInfo? ClientUpdateMethod
        => AccessTools.Method(AccessTools.TypeByName(ClientCalendarTypeName), "Update");

    public bool Start(ICoreAPI api, NearBodyLightController lightController)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(lightController);

        controller = lightController;
        if (applied)
        {
            return true;
        }

        if (DayLightStrengthMethod is not { } dayLight)
        {
            api.Logger.Warning(
                "AstraTerra could not find GameCalendar.GetDayLightStrength; a parent giant will be drawn "
                + "but will not light the ground or eclipse the sun.");
            return false;
        }

        harmony.Patch(
            dayLight,
            postfix: new HarmonyMethod(
                AccessTools.Method(typeof(NearBodyLightPatchHost), nameof(DayLightStrengthPostfix))));

        // Absent on a dedicated server, which has no client calendar and needs none: nothing there
        // renders, and the gameplay light is already covered by the patch above.
        if (ClientUpdateMethod is { } clientUpdate)
        {
            harmony.Patch(
                clientUpdate,
                postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(NearBodyLightPatchHost), nameof(ClientUpdatePostfix))));
        }

        applied = true;
        return true;
    }

    public void Stop()
    {
        if (applied)
        {
            harmony.UnpatchAll(harmony.Id);
            applied = false;
        }

        controller = null;
    }

    /// <summary>
    /// Adds the giant's light to, and takes its eclipse out of, every time-of-day light query.
    /// </summary>
    /// <remarks>
    /// Wrapped, because this runs inside the block-lighting path: a throw here would take out chunk
    /// lighting rather than one body's appearance, and a world lit by vanilla alone is a far better
    /// failure than a world that cannot light itself at all.
    /// </remarks>
    public static void DayLightStrengthPostfix(object __instance, double x, double z, ref float __result)
    {
        if (controller is not { IsActive: true } active || __instance is not IGameCalendar calendar)
        {
            return;
        }

        try
        {
            __result = active.Apply(calendar, x, z, __result);
        }
        catch (Exception)
        {
            // Leave the vanilla value standing.
        }
    }

    /// <summary>
    /// Brings the client's rendering copy of day-light strength in line with the gameplay one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read back through the same controller rather than recomputed, so the sky the player sees and
    /// the light the world runs on are literally the same number.
    /// </para>
    /// <para>
    /// Written through the concrete property rather than the interface, because
    /// <see cref="IClientGameCalendar.DayLightStrength"/> is read-only to mods -- the game sets it
    /// once a frame and expects nobody else to. Setting it after that frame's update has finished
    /// is the whole point, so the setter is reached the only way it can be.
    /// </para>
    /// </remarks>
    public static void ClientUpdatePostfix(object __instance)
    {
        if (controller is not { IsActive: true } active
            || __instance is not IGameCalendar calendar
            || DayLightSetter is not { } setter
            || DayLightGetter is not { } getter)
        {
            return;
        }

        try
        {
            var (x, z) = active.ObserverPosition();
            setter(__instance, active.Apply(calendar, x, z, (float)getter(__instance)!));
        }
        catch (Exception)
        {
            // Leave the vanilla value standing.
        }
    }

    private static System.Action<object, float>? dayLightSetter;
    private static System.Func<object, object?>? dayLightGetter;

    private static System.Action<object, float>? DayLightSetter
        => dayLightSetter ??= BuildDayLightSetter();

    private static System.Func<object, object?>? DayLightGetter
        => dayLightGetter ??= BuildDayLightGetter();

    private static System.Action<object, float>? BuildDayLightSetter()
    {
        var property = AccessTools.Property(AccessTools.TypeByName(ClientCalendarTypeName), "DayLightStrength");
        var setter = property?.GetSetMethod();
        return setter is null ? null : (instance, value) => setter.Invoke(instance, [value]);
    }

    private static System.Func<object, object?>? BuildDayLightGetter()
    {
        var property = AccessTools.Property(AccessTools.TypeByName(ClientCalendarTypeName), "DayLightStrength");
        var getter = property?.GetGetMethod();
        return getter is null ? null : instance => getter.Invoke(instance, []);
    }
}
