namespace AstraTerra.Config;

public sealed class AstraTerraConfig
{
    public const double DefaultDebugMeteorRateMultiplier = 1.0;
    public const double MaximumDebugMeteorRateMultiplier = 100.0;

    public string StarfieldMode { get; set; } = StarfieldModeParser.AstraTerraValue;
    public string SkyGridMode { get; set; } = SkyGridModeParser.NoneValue;
    public float StarBrightnessBias { get; set; } = 1.0f;
    public float GuideStarHighlightStrength { get; set; } = 1.15f;
    public float SelectionSnapRadiusDeg { get; set; } = 1.0f;
    public bool ShowMinimalHud { get; set; } = true;
    public bool ShowReticle { get; set; } = true;
    public bool DebugGuideStarEmphasisDefault { get; set; }
    public double DebugMeteorRateMultiplier { get; set; } = DefaultDebugMeteorRateMultiplier;

    public StarfieldMode GetStarfieldMode()
        => StarfieldModeParser.ParseOrDefault(StarfieldMode);

    public SkyGridMode GetSkyGridMode()
        => SkyGridModeParser.ParseOrDefault(SkyGridMode);

    public double GetDebugMeteorRateMultiplier()
        => double.IsFinite(DebugMeteorRateMultiplier)
            ? Math.Clamp(
                DebugMeteorRateMultiplier,
                0.0,
                MaximumDebugMeteorRateMultiplier)
            : DefaultDebugMeteorRateMultiplier;
}
