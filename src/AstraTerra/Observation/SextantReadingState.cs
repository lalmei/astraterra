using AstraTerra.Config;

namespace AstraTerra.Observation;

public static class SextantReadingState
{
    public static bool IsReading { get; private set; }
    public static SkyGridMode GridMode { get; private set; } = SkyGridMode.None;

    public static void Begin()
    {
        IsReading = true;
    }

    public static void End()
    {
        IsReading = false;
    }

    public static SkyGridMode CycleGridMode()
    {
        GridMode = GridMode switch
        {
            SkyGridMode.None => SkyGridMode.Equatorial,
            SkyGridMode.Equatorial => SkyGridMode.Horizontal,
            SkyGridMode.Horizontal => SkyGridMode.Both,
            _ => SkyGridMode.None
        };

        return GridMode;
    }

    public static void Reset()
    {
        IsReading = false;
        GridMode = SkyGridMode.None;
    }
}
