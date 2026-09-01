namespace AstraTerra.Config;

public sealed class AstraTerraConfig
{
    public const double DefaultDebugMeteorRateMultiplier = 1.0;
    public const double MaximumDebugMeteorRateMultiplier = 100.0;
    public const float DefaultMilkyWayBrightness = 1.0f;
    public const float MaximumMilkyWayBrightness = 2.0f;

    public string StarfieldMode { get; set; } = StarfieldModeParser.AstraTerraValue;
    public string SkyGridMode { get; set; } = SkyGridModeParser.NoneValue;

    /// <summary>
    /// Which set of pictures the sun's family is drawn from: the pixel art, or the photographs.
    /// </summary>
    public string SolarSystemArt { get; set; } = SolarSystemArtStyleParser.PixelValue;

    /// <summary>
    /// How much of Vintage Story's own date and hour the character panel keeps showing. Defaults to
    /// showing all of it; the disc only becomes an instrument once this is turned down.
    /// </summary>
    public string CalendarDisplay { get; set; } = CalendarDisplayParser.FullValue;

    /// <summary>
    /// When false, vanilla's sun ignores longitude and the whole world shares one time zone.
    /// </summary>
    public bool LongitudeAwareSun { get; set; } = true;
    public float StarBrightnessBias { get; set; } = 1.0f;

    /// <summary>
    /// How strongly the Milky Way's glow draws, as a multiplier on the mod's own night-adjusted
    /// value. Zero switches the band off for players who would rather have a plain star field.
    /// </summary>
    public float MilkyWayBrightness { get; set; } = DefaultMilkyWayBrightness;
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

    public SolarSystemArtStyle GetSolarSystemArtStyle()
        => SolarSystemArtStyleParser.ParseOrDefault(SolarSystemArt);

    public CalendarDisplay GetCalendarDisplay()
        => CalendarDisplayParser.ParseOrDefault(CalendarDisplay);

    /// <summary>The Milky Way multiplier, clamped and with a non-finite setting treated as absent.</summary>
    public float GetMilkyWayBrightness()
        => float.IsFinite(MilkyWayBrightness)
            ? Math.Clamp(MilkyWayBrightness, 0f, MaximumMilkyWayBrightness)
            : DefaultMilkyWayBrightness;

    public double GetDebugMeteorRateMultiplier()
        => double.IsFinite(DebugMeteorRateMultiplier)
            ? Math.Clamp(
                DebugMeteorRateMultiplier,
                0.0,
                MaximumDebugMeteorRateMultiplier)
            : DefaultDebugMeteorRateMultiplier;
}
