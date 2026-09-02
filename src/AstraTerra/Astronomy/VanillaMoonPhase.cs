using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

/// <summary>
/// The phase Vintage Story's moon is in at an instant other than right now.
/// </summary>
/// <remarks>
/// <see cref="IGameCalendar"/> reports the phase only for the current moment, which is not enough
/// once the moon is read at the observer's own time rather than the world's. The concrete calendar
/// can answer for any instant, so it is asked directly and its own brightness table is interpolated
/// exactly as its property does — this is the game's phase at another time, not a second moon model.
/// Anything else providing the calendar keeps the world's current reading.
/// </remarks>
public static class VanillaMoonPhase
{
    public static double ExactAt(IGameCalendar calendar, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return calendar is Vintagestory.Common.GameCalendar gameCalendar
            ? gameCalendar.GetMoonPhase(totalDays)
            : calendar.MoonPhaseExact;
    }

    public static float BrightnessAt(IGameCalendar calendar, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (calendar is not Vintagestory.Common.GameCalendar gameCalendar)
        {
            return calendar.MoonPhaseBrightness;
        }

        var brightnessByPhase = Vintagestory.Common.GameCalendar.MoonBrightnesByPhase;
        if (brightnessByPhase is not { Length: > 0 })
        {
            return calendar.MoonPhaseBrightness;
        }

        var orbitDays = gameCalendar.MoonOrbitDays > 0
            ? Math.Min(gameCalendar.MoonOrbitDays, brightnessByPhase.Length)
            : brightnessByPhase.Length;
        var phase = gameCalendar.GetMoonPhase(totalDays);
        if (!double.IsFinite(phase))
        {
            return calendar.MoonPhaseBrightness;
        }

        var wrapped = phase % orbitDays;
        if (wrapped < 0)
        {
            wrapped += orbitDays;
        }

        var index = (int)wrapped;
        var toNext = (float)(wrapped - index);
        var current = brightnessByPhase[index % orbitDays];
        var next = brightnessByPhase[(index + 1) % orbitDays];
        return (current * (1f - toNext)) + (next * toNext);
    }
}
