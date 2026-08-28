using Vintagestory.API.Client;

namespace AstraTerra.Config;

public static class AstraTerraConfigLoader
{
    private const string ConfigName = "astraterra.json";

    public static AstraTerraConfig Load(ICoreClientAPI api)
    {
        var config = api.LoadModConfig<AstraTerraConfig>(ConfigName) ?? new AstraTerraConfig();
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
        if (!CalendarDisplayParser.TryParse(config.CalendarDisplay, out var calendarDisplay))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown calendar display '{0}'; using '{1}'.",
                config.CalendarDisplay,
                CalendarDisplayParser.FullValue);
        }

        config.CalendarDisplay = CalendarDisplayParser.ToConfigValue(calendarDisplay);
        var normalizedMeteorRateMultiplier = config.GetDebugMeteorRateMultiplier();
        if (normalizedMeteorRateMultiplier != config.DebugMeteorRateMultiplier)
        {
            api.Logger.Warning(
                "AstraTerra config has invalid debug meteor rate multiplier '{0}'; using '{1}'.",
                config.DebugMeteorRateMultiplier,
                normalizedMeteorRateMultiplier);
        }

        config.DebugMeteorRateMultiplier = normalizedMeteorRateMultiplier;
        Store(api, config);
        return config;
    }

    public static void Store(ICoreClientAPI api, AstraTerraConfig config)
    {
        api.StoreModConfig(config, ConfigName);
    }
}
