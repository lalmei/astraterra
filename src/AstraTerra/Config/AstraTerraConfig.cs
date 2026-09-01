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
    /// Which set of pictures the planets and their moons are drawn from: the pixel art, or the
    /// photographs. The moon overhead is <see cref="MoonArt"/>.
    /// </summary>
    public string SolarSystemArt { get; set; } = SolarSystemArtStyleParser.PixelValue;

    /// <summary>
    /// Which picture the moon overhead is drawn from: the pixel art, the photographs, or Vintage
    /// Story's own disc. Independent of <see cref="SolarSystemArt"/>, which is the planets.
    /// </summary>
    public string MoonArt { get; set; } = MoonArtStyleParser.PixelValue;

    /// <summary>
    /// How much of Vintage Story's own date and hour the character panel keeps showing. Defaults to
    /// showing all of it; the disc only becomes an instrument once this is turned down.
    /// </summary>
    public string CalendarDisplay { get; set; } = CalendarDisplayParser.FullValue;

    /// <summary>
    /// When false, vanilla's sun ignores longitude and the whole world shares one time zone.
    /// </summary>
    public bool LongitudeAwareSun { get; set; } = true;

    /// <summary>
    /// Which hour the player sees on the character panel and instruments. The world's stored clock
    /// remains universal regardless.
    /// </summary>
    public string DisplayedClockTime { get; set; } = DisplayedClockTimeParser.LocalSolarValue;
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

    public MoonArtStyle GetMoonArtStyle()
        => MoonArtStyleParser.ParseOrDefault(MoonArt);

    public CalendarDisplay GetCalendarDisplay()
        => CalendarDisplayParser.ParseOrDefault(CalendarDisplay);

    public DisplayedClockTime GetDisplayedClockTime()
        => DisplayedClockTimeParser.ParseOrDefault(DisplayedClockTime);

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
