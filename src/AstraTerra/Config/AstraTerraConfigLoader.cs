using Vintagestory.API.Client;

namespace AstraTerra.Config;

public static class AstraTerraConfigLoader
{
    private const string ConfigName = "astraterra.json";

    public static AstraTerraConfig Load(ICoreClientAPI api)
    {
        var config = api.LoadModConfig<AstraTerraConfig>(ConfigName) ?? new AstraTerraConfig();
        api.StoreModConfig(config, ConfigName);
        return config;
    }
}
