using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

/// <summary>
/// World-configuration values that Vintage Story uses for its climate bands and that AstraTerra
/// should reuse so latitude and longitude stay on the same ground scale.
/// </summary>
public static class WorldClimateScale
{
    public const string PolarEquatorDistanceKey = "polarEquatorDistance";
    public const double DefaultPolarEquatorDistance = 50000.0;

    /// <summary>
    /// Blocks from the equator to a pole, matching <c>OnGetLatitude</c>. Falls back to 50,000 when
    /// the world config is missing or unreadable.
    /// </summary>
    public static double GetPolarEquatorDistance(IWorldAccessor world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var config = world.Config;
        if (config is null)
        {
            return DefaultPolarEquatorDistance;
        }

        var configured = config.GetInt(PolarEquatorDistanceKey, -1);
        if (configured > 0)
        {
            return configured;
        }

        var configuredText = config.GetString(PolarEquatorDistanceKey, null);
        if (configuredText is not null
            && int.TryParse(configuredText, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return DefaultPolarEquatorDistance;
    }
}
