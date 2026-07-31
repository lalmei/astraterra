using Vintagestory.API.Client;

namespace AstraTerra.Config;

public static class AstraTerraConfigLoader
{
    private const string ConfigName = "astraterra.json";

    public static AstraTerraConfig Load(ICoreClientAPI api)
    {
        var config = api.LoadModConfig<AstraTerraConfig>(ConfigName) ?? new AstraTerraConfig();
        if (!SkyGridModeParser.TryParse(config.SkyGridMode, out var skyGridMode))
        {
            api.Logger.Warning(
                "AstraTerra config has unknown sky grid mode '{0}'; using '{1}'.",
                config.SkyGridMode,
                SkyGridModeParser.NoneValue);
        }

        config.SkyGridMode = SkyGridModeParser.ToConfigValue(skyGridMode);
        Store(api, config);
        return config;
    }

    public static void Store(ICoreClientAPI api, AstraTerraConfig config)
    {
        api.StoreModConfig(config, ConfigName);
    }
}
