using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Config;

public static class AstraTerraConfigLoader
{
    private const string ConfigName = "astraterra.json";

    public static AstraTerraConfig Load(ICoreAPI api)
    {
        var config = api.LoadModConfig<AstraTerraConfig>(ConfigName) ?? new AstraTerraConfig();
        Normalize(config, api);
        api.StoreModConfig(config, ConfigName);

        return config;
    }

    public static AstraTerraConfig Load(ICoreClientAPI api)
        => Load((ICoreAPI)api);

    public static void Store(ICoreClientAPI api, AstraTerraConfig config)
    {
        api.StoreModConfig(config, ConfigName);
    }

    private static void Normalize(AstraTerraConfig config, ICoreAPI api)
    {
        if (!StarfieldModeParser.TryParse(config.StarfieldMode, out var starfieldMode))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown starfield mode '{0}'; using '{1}'.",
                config.StarfieldMode,
                StarfieldModeParser.AstraTerraValue);
        }

        config.StarfieldMode = StarfieldModeParser.ToConfigValue(starfieldMode);
        if (!SkyGridModeParser.TryParse(config.SkyGridMode, out var skyGridMode))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown sky grid mode '{0}'; using '{1}'.",
                config.SkyGridMode,
                SkyGridModeParser.NoneValue);
        }

        config.SkyGridMode = SkyGridModeParser.ToConfigValue(skyGridMode);
        if (!SolarSystemArtStyleParser.TryParse(config.SolarSystemArt, out var solarSystemArt))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown solar system art '{0}'; using '{1}'.",
                config.SolarSystemArt,
                SolarSystemArtStyleParser.PixelValue);
        }

        config.SolarSystemArt = SolarSystemArtStyleParser.ToConfigValue(solarSystemArt);
        if (!MoonArtStyleParser.TryParse(config.MoonArt, out var moonArt))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown moon art '{0}'; using '{1}'.",
                config.MoonArt,
                MoonArtStyleParser.PixelValue);
        }

        config.MoonArt = MoonArtStyleParser.ToConfigValue(moonArt);
        if (!CalendarDisplayParser.TryParse(config.CalendarDisplay, out var calendarDisplay))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown calendar display '{0}'; using '{1}'.",
                config.CalendarDisplay,
                CalendarDisplayParser.FullValue);
        }

        config.CalendarDisplay = CalendarDisplayParser.ToConfigValue(calendarDisplay);
        if (!DisplayedClockTimeParser.TryParse(config.DisplayedClockTime, out var displayedClockTime))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown displayed clock time '{0}'; using '{1}'.",
                config.DisplayedClockTime,
                DisplayedClockTimeParser.LocalSolarValue);
        }

        config.DisplayedClockTime = DisplayedClockTimeParser.ToConfigValue(displayedClockTime);
        var normalizedMeteorRateMultiplier = config.GetDebugMeteorRateMultiplier();
        if (normalizedMeteorRateMultiplier != config.DebugMeteorRateMultiplier)
        {
            api.Logger.Warning(
                "AstraTerra config has invalid debug meteor rate multiplier '{0}'; using '{1}'.",
                config.DebugMeteorRateMultiplier,
                normalizedMeteorRateMultiplier);
        }

        config.DebugMeteorRateMultiplier = normalizedMeteorRateMultiplier;
    }
}
