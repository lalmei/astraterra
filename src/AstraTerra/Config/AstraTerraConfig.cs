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
    /// Story's own disc. Independent of <see cref="SolarSystemArt"/>, which is the planets. Defaults
    /// to Vintage Story's disc for now.
    /// </summary>
    public string MoonArt { get; set; } = MoonArtStyleParser.DefaultValue;

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
    /// Whether a generated parent giant lights the world it hangs over, and eclipses its sun.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one AstraTerra setting that changes the world rather than the view of it. A
    /// tidally locked moon's giant is tens of degrees wide and full at local midnight, so its
    /// nights are genuinely lit -- and because a regular satellite orbits in its giant's equatorial
    /// plane, the sun passes behind that giant twice a year, which is an eclipse season. With this
    /// on, both reach the light the world runs on: what spawns at night, how crops grow, how cold
    /// it gets.
    /// </para>
    /// <para>
    /// Only a moon world has a parent giant, so on every other world this setting does nothing at
    /// all. The server's value applies to everyone, the same way the longitude-aware sun's does,
    /// because the light a dedicated server spawns mobs by has to be the light its clients see.
    /// </para>
    /// </remarks>
    public bool NearBodyLighting { get; set; } = true;

    /// <summary>
    /// Whether a generated world may run on its own axial tilt rather than Earth's 23.4 degrees.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A tidally locked moon does not pick its own tilt: it is locked to its giant and orbits in
    /// that giant's equatorial plane, so its axis is the giant's axis. A moon of a Jupiter-analog
    /// therefore has almost no seasons and is eclipsed nearly every day; a moon of a Saturn-analog
    /// has proper seasons and two eclipse seasons a year. With this off, every world keeps Earth's
    /// tilt and every generated moon reads the same.
    /// </para>
    /// <para>
    /// This changes the world rather than the view of it -- day length by latitude and season, and
    /// through them what grows -- so like the near-body light it is the server's to decide and its
    /// value is sent to every client. Vintage Story's own seasonal temperature curve does not
    /// follow the tilt, which is why the generator handing the tilt over is expected to keep it
    /// inside a habitable band rather than passing on a world lying on its side.
    /// </para>
    /// </remarks>
    public bool GeneratedWorldTilt { get; set; } = true;

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
