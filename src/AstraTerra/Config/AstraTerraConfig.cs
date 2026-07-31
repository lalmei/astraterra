namespace AstraTerra.Config;

public sealed class AstraTerraConfig
{
    public string SkyGridMode { get; set; } = SkyGridModeParser.NoneValue;
    public float StarBrightnessBias { get; set; } = 1.0f;
    public float GuideStarHighlightStrength { get; set; } = 1.15f;
    public float SelectionSnapRadiusDeg { get; set; } = 1.0f;
    public bool ShowMinimalHud { get; set; } = true;
    public bool ShowReticle { get; set; } = true;
    public bool DebugGuideStarEmphasisDefault { get; set; }

    public SkyGridMode GetSkyGridMode()
        => SkyGridModeParser.ParseOrDefault(SkyGridMode);
}
