using AstraTerra.Config;

namespace AstraTerra.Client.Rendering;

public static class SkyGridDisplayPolicy
{
    public static SkyGridMode Resolve(SkyGridMode configuredMode, bool sextantInUse)
        => sextantInUse ? SkyGridMode.Both : configuredMode;
}
