using AstraTerra.Astronomy;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed class AstrolabePlannerRenderer : IRenderer
{
    private const float TitleTopOffset = 48.0f;
    private const float ReadingTopOffset = 86.0f;
    private const float ClockTopOffset = 118.0f;
    private const float HelpTopOffset = 150.0f;

    /// <summary>How far the player must travel before the sky clock is worth recomputing.</summary>
    private const double ClockCacheBlockSize = 32.0;

    /// <summary>
    /// The item is called a Calibrated Astrolabe, but the HUD cannot be: a freshly crafted one is
    /// not calibrated, and "Calibrated Astrolabe — no plate" is nonsense. The plain noun reads
    /// correctly in both states, and the plate line says which one this is.
    /// </summary>
    private const string InstrumentTitle = "Astrolabe";

    private const string CalibrationHelp =
        "Sneak + hold right click under an open night sky to cut its plate for this latitude.";

    /// <summary>Characters across the sighting progress bar.</summary>
    private const int ProgressBarWidth = 24;

    private readonly ICoreClientAPI api;
    private StarCatalog catalog;
    private readonly ConstellationBookClient bookClient;
    private PlanetCatalog? planetCatalog;
    private CometCatalog? cometCatalog;
    private Dictionary<string, CometEntry> cometsById;
    private IReadOnlyList<AstrolabeTarget>? planetTargets;
    private IReadOnlyList<AstrolabeTarget>? cometTargets;
    private int cometTargetsDaysPerYear;
    private double cometTargetsHoursPerDay;
    private int planetTargetsDaysPerYear;
    private double planetTargetsHoursPerDay;
    private LoadedTexture? titleTexture;
    private LoadedTexture? readingTexture;
    private LoadedTexture? clockTexture;
    private LoadedTexture? helpTexture;
    private string? titleText;
    private string? readingText;
    private string? clockText;
    private string? helpText;
    private string? cachedClockLine;
    private long cachedClockMinute;
    private int cachedClockPositionKey;
    private bool renderingDisabledAfterFailure;

    /// <param name="planetCatalog">Null when the planet catalog failed to load. Constellations still plan.</param>
    /// <param name="cometCatalog">Null when the comet catalog failed to load. Everything else still plans.</param>
    public AstrolabePlannerRenderer(
        ICoreClientAPI api,
        StarCatalog catalog,
        ConstellationBookClient bookClient,
        PlanetCatalog? planetCatalog = null,
        CometCatalog? cometCatalog = null)
    {
        this.api = api;
        this.catalog = catalog;
        this.bookClient = bookClient;
        this.planetCatalog = planetCatalog;
        this.cometCatalog = cometCatalog;
        cometsById = cometCatalog?.Comets.ToDictionary(comet => comet.Id, StringComparer.Ordinal)
                     ?? new Dictionary<string, CometEntry>(StringComparer.Ordinal);
    }

    public double RenderOrder => 0.99;

    public int RenderRange => 9999;

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplaceCatalog"/>.</summary>
    public void ReplaceCatalog(StarCatalog replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        catalog = replacement;
    }

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplacePlanetCatalog"/>.</summary>
    public void ReplacePlanetCatalog(PlanetCatalog? replacement)
    {
        planetCatalog = replacement;
        planetTargets = null;
        planetTargetsDaysPerYear = 0;
        planetTargetsHoursPerDay = 0;
    }

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplaceCometCatalog"/>.</summary>
    public void ReplaceCometCatalog(CometCatalog? replacement)
    {
        cometCatalog = replacement;
        cometsById = replacement?.Comets.ToDictionary(comet => comet.Id, StringComparer.Ordinal)
                     ?? new Dictionary<string, CometEntry>(StringComparer.Ordinal);
        cometTargets = null;
        cometTargetsDaysPerYear = 0;
        cometTargetsHoursPerDay = 0;
    }

    public void OnMouseDown(MouseEvent args)
    {
        if (!IsReadingAstrolabe() || args.Button != EnumMouseButton.Middle)
        {
            return;
        }

        AstrolabeReadingState.CycleTarget(GetCurrentTargets().Count);
        args.Handled = true;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Ortho || renderingDisabledAfterFailure || !IsReadingAstrolabe())
        {
            return;
        }

        try
        {
            RenderPlanner();
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            api.Logger.Warning("AstraTerra disabled astrolabe planning HUD after an unexpected error: {0}", exception);
        }
    }

    public void Dispose()
    {
        DeleteTexture(titleTexture);
        DeleteTexture(readingTexture);
        DeleteTexture(clockTexture);
        DeleteTexture(helpTexture);
    }

    private void RenderPlanner()
    {
        if (AstrolabeCalibrationState.IsCalibrating)
        {
            RenderCalibration();
            return;
        }

        // Every reading below is answered for the plate, not for where the player is standing. An
        // astrolabe that has never been sighted has no plate and can place nothing.
        var plateLatitude = AstrolabeCalibrationStore.ReadLatitude(
            api.World.Player.InventoryManager?.ActiveHotbarSlot?.Itemstack);
        if (plateLatitude is null)
        {
            RenderLines(
                $"{InstrumentTitle} — no plate",
                "Nothing is engraved on this astrolabe yet, so it cannot place a star.",
                ReadSkyClockLine(api.World.Calendar.TotalDays),
                CalibrationHelp);
            return;
        }

        // Any book of ours will do, not only one with figures drawn in it: a book holding nothing
        // but sightings of a wanderer is a model of the sky, and this instrument runs on that model.
        if (!bookClient.HasLeftHandJournalBook())
        {
            RenderLines(
                InstrumentTitle,
                "Hold a written constellation book in your left hand.",
                ReadSkyClockLine(api.World.Calendar.TotalDays),
                string.Empty);
            return;
        }

        var targets = BuildTargets(bookClient.ReadCurrentJournalOrEmpty());
        if (targets.Count == 0)
        {
            RenderLines(
                InstrumentTitle,
                "Nothing in this book can be aimed at yet: no figures drawn, and no wanderer picked out.",
                ReadSkyClockLine(api.World.Calendar.TotalDays),
                string.Empty);
            return;
        }

        var calendar = api.World.Calendar;
        var hoursPerDay = Math.Max(1.0, calendar.HoursPerDay);
        var daysPerYear = Math.Max(1, calendar.DaysPerYear);
        var forecastTotalDays = calendar.TotalDays + (AstrolabeReadingState.ForecastHours / hoursPerDay);
        var position = api.World.Player.Entity.Pos;
        var latitude = plateLatitude.Value;

        // Longitude is not engraved on a plate — it only shifts what hour a star transits, not where
        // the horizon falls — so the instrument keeps reading it live wherever it is carried.
        var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World);
        var selectedIndex = AstrolabeReadingState.NormalizeTargetIndex(targets.Count);
        var selectedTarget = targets[selectedIndex];
        var awayComet = FormatAwayComet(selectedTarget, forecastTotalDays, daysPerYear);
        var reading = AstrolabeService.Read(
            targets[selectedIndex],
            latitude,
            forecastTotalDays,
            daysPerYear,
            hoursPerDay,
            longitude);

        var forecastDay = PositiveModulo((int)Math.Floor(forecastTotalDays), daysPerYear) + 1;
        var plate = AstrolabeCalibrationPolicy.DescribePlate(latitude, ObserverLatitude());
        var title = $"{InstrumentTitle} — {FormatForecastOffset(AstrolabeReadingState.ForecastHours, hoursPerDay)} — {plate} — day {forecastDay}/{daysPerYear}";
        var provenance = string.IsNullOrWhiteSpace(reading.Target.Provenance)
            ? string.Empty
            : $" · {reading.Target.Provenance}";
        var details = $"{FormatTargetName(reading.Target)} ({selectedIndex + 1}/{targets.Count}) — "
                      + $"{awayComet ?? FormatReading(reading)}{provenance}";
        var help = "Middle click: next target   Scroll: 1 hour   Sneak + scroll: 7 days   Sneak + right click: recut the plate";
        RenderLines(title, details, ReadSkyClockLine(forecastTotalDays), help);
    }

    /// <summary>
    /// What the HUD shows while the plate is being cut: how far along the sighting is, or what is
    /// stopping it. The sky clock stays on screen throughout, because the one thing an astrolabe
    /// can always tell you is the hour.
    /// </summary>
    private void RenderCalibration()
    {
        var obstacle = AstrolabeCalibrationState.Obstacle;
        var reading = obstacle
                      ?? $"{FormatProgressBar(AstrolabeCalibrationState.Progress)}  sighting the pole at {AstrolabeCalibrationPolicy.FormatLatitude(ObserverLatitude())}";

        RenderLines(
            $"{InstrumentTitle} — cutting the plate",
            reading,
            ReadSkyClockLine(api.World.Calendar.TotalDays),
            obstacle is null
                ? "Keep sneaking and holding right click."
                : CalibrationHelp);
    }

    /// <summary>Where the player actually is, which is only the same as the plate until they travel.</summary>
    private double ObserverLatitude()
    {
        var calendar = api.World.Calendar;
        return LatitudeMapper.MapGameLatitude(
            api.World.Player.Entity.Pos.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
    }

    private static string FormatProgressBar(float progress)
    {
        var filled = (int)Math.Round(Math.Clamp(progress, 0f, 1f) * ProgressBarWidth);
        return $"[{new string('\u2588', filled)}{new string('\u00b7', ProgressBarWidth - filled)}]";
    }

    /// <summary>
    /// The astrolabe's other half: reading the hour off the sky. The sun altitude comes from
    /// Vintage Story so the hour lines up with the daylight the player can see, and it is sampled
    /// at the forecast time so scrolling ahead moves the clock with the stars.
    /// </summary>
    private string ReadSkyClockLine(double totalDays)
    {
        var calendar = api.World.Calendar;
        var hoursPerDay = Math.Max(1.0, calendar.HoursPerDay);
        var position = api.World.Player.Entity.Pos.XYZ;

        // Finding the next sunrise and sunset costs a few hundred sun samples, but the line only
        // changes once a world minute, and this runs every frame while the astrolabe is raised.
        // Recompute on the minute, or once the player has travelled far enough to move the sun.
        var minute = (long)Math.Floor(totalDays * hoursPerDay * 60.0);
        var positionKey = HashCode.Combine(
            (long)Math.Floor(position.X / ClockCacheBlockSize),
            (long)Math.Floor(position.Z / ClockCacheBlockSize));
        if (cachedClockLine is not null && minute == cachedClockMinute && positionKey == cachedClockPositionKey)
        {
            return cachedClockLine;
        }

        var clock = SkyClock.Read(
            totalDays,
            hoursPerDay,
            days => SunAltitudeDegAt(position, days),
            longitudeDegrees: ClockDisplay.MapWorldLongitude(position.X, api.World));
        cachedClockLine = FormatSkyClock(clock, hoursPerDay, totalDays);
        cachedClockMinute = minute;
        cachedClockPositionKey = positionKey;
        return cachedClockLine;
    }

    private double SunAltitudeDegAt(Vec3d position, double totalDays)
    {
        var vector = api.World.Calendar.GetSunPosition(position, totalDays);
        return SkyBodyModel.FromWorldDirection("Sun", vector.X, vector.Y, vector.Z)?.AltitudeDeg ?? 0.0;
    }

    private string FormatSkyClock(SkyClockReading clock, double hoursPerDay, double totalDays)
    {
        var phase = clock.Phase switch
        {
            SkyPhase.Day => "daylight",
            SkyPhase.Dusk => "dusk",
            SkyPhase.Night => "night",
            SkyPhase.Dawn => "dawn",
            _ => "unknown"
        };

        var next = clock.Phase == SkyPhase.Day
            ? clock.HoursUntilSunset is { } sunset
                ? $"sunset in {sunset:0.0} h"
                : "the sun does not set"
            : clock.HoursUntilSunrise is { } sunrise
                ? $"sunrise in {sunrise:0.0} h"
                : "the sun does not rise";

        var timeLine = FormatClockTime(clock.LocalTimeHours, hoursPerDay);
        var universalHours = CelestialMath.GetUniversalSolarTimeHours(totalDays, hoursPerDay);
        if (Math.Abs(clock.LocalTimeHours - universalHours) > 0.05)
        {
            timeLine += $" ({FormatClockTime(universalHours, hoursPerDay)} world)";
        }

        return $"{timeLine} — {phase}, sun {clock.SunAltitudeDeg:+0.0;-0.0;0.0}°, {next}";
    }

    private static string FormatClockTime(double localTimeHours, double hoursPerDay)
    {
        var wrapped = Math.Clamp(localTimeHours, 0.0, hoursPerDay);
        var hours = (int)Math.Floor(wrapped);
        var minutes = (int)Math.Floor((wrapped - hours) * 60.0);
        if (minutes >= 60)
        {
            minutes = 59;
        }

        return $"{hours:00}:{minutes:00}";
    }

    /// <summary>
    /// Labels a planet as one, and calls it whatever the held book calls it. Resolved here rather
    /// than baked into the target so swapping books renames the sky immediately.
    /// </summary>
    private string FormatTargetName(AstrolabeTarget target)
    {
        // A comet keeps its catalogued name without a book, where a planet does not. A wandering
        // star is a discovery an observer makes by watching one; a comet is an event, and the
        // catalog authors the name it arrives under.
        if (target.Kind == AstrolabeTargetKind.Comet)
        {
            return $"{target.DisplayName} · comet";
        }

        if (target.Kind != AstrolabeTargetKind.Planet)
        {
            return target.DisplayName;
        }

        var journal = ConstellationBookService.ReadPlanetJournalOrEmpty(
            api.World.Player.Entity.LeftHandItemSlot?.Itemstack);
        return $"{journal.DisplayName(target.SourceId)} · planet";
    }

    private IReadOnlyList<AstrolabeTarget> GetCurrentTargets()
    {
        var journal = bookClient.ReadCurrentJournal();
        return journal is null ? [] : BuildTargets(journal);
    }

    /// <summary>
    /// The recorded figures the book holds, followed by the wandering planets.
    /// </summary>
    /// <remarks>
    /// Constellations keep their existing order and position in the list, so middle click still lands
    /// where a player expects before it reaches the planets.
    /// </remarks>
    private IReadOnlyList<AstrolabeTarget> BuildTargets(ConstellationJournal journal)
    {
        // Resolved from the held book every time rather than cached with the targets, so putting a
        // different book in your off-hand changes what the instrument can aim at, immediately.
        var stack = api.World.Player.Entity.LeftHandItemSlot?.Itemstack;
        var observations = ConstellationBookService.ReadObservationLogOrEmpty(stack);
        var planetJournal = ConstellationBookService.ReadPlanetJournalOrEmpty(stack);

        return
        [
            .. AstrolabeJournalTargets.Drawn(AstrolabeService.BuildTargets(journal, catalog)),
            .. AstrolabeJournalTargets.KnownWanderers(ResolvePlanetTargets(), observations, planetJournal),
            .. AstrolabeJournalTargets.FromAlmanac(ResolveCometTargets())
        ];
    }

    /// <summary>
    /// Built once and kept. Each planet target owns a cached ephemeris, and the astrolabe asks about
    /// forecast times while the sky renderer asks about now — rebuilding per frame would throw the
    /// cache away and re-solve five orbits every frame the instrument is raised.
    /// </summary>
    private IReadOnlyList<AstrolabeTarget> ResolvePlanetTargets()
    {
        if (planetCatalog is null)
        {
            return [];
        }

        var daysPerYear = Math.Max(1, api.World.Calendar.DaysPerYear);
        var hoursPerDay = Math.Max(1.0, api.World.Calendar.HoursPerDay);
        if (planetTargets is null
            || daysPerYear != planetTargetsDaysPerYear
            || Math.Abs(hoursPerDay - planetTargetsHoursPerDay) > 1e-9)
        {
            planetTargets = AstrolabeService.BuildPlanetTargets(planetCatalog, daysPerYear, hoursPerDay);
            planetTargetsDaysPerYear = daysPerYear;
            planetTargetsHoursPerDay = hoursPerDay;
        }

        return planetTargets;
    }

    /// <summary>
    /// Built once and kept, like the planet targets: each comet's ephemeris caches its last sample,
    /// and rebuilding per frame would throw that away.
    /// </summary>
    private IReadOnlyList<AstrolabeTarget> ResolveCometTargets()
    {
        if (cometCatalog is null)
        {
            return [];
        }

        var daysPerYear = Math.Max(1, api.World.Calendar.DaysPerYear);
        var hoursPerDay = Math.Max(1.0, api.World.Calendar.HoursPerDay);
        if (cometTargets is null
            || daysPerYear != cometTargetsDaysPerYear
            || Math.Abs(hoursPerDay - cometTargetsHoursPerDay) > 1e-9)
        {
            cometTargets = AstrolabeService.BuildCometTargets(cometCatalog, daysPerYear, hoursPerDay);
            cometTargetsDaysPerYear = daysPerYear;
            cometTargetsHoursPerDay = hoursPerDay;
        }

        return cometTargets;
    }

    /// <summary>
    /// The line for a comet that is not here at the forecast time, or null for anything that is.
    /// </summary>
    /// <remarks>
    /// A comet away from perihelion has no position worth reporting — its authored track only covers
    /// the apparition — so reporting a compass bearing and an altitude for one would be inventing a
    /// place for it to be. What it does have is a date, and the forecast dial is already the way a
    /// player reaches other dates: scroll far enough ahead and this line gives way to a real reading.
    /// </remarks>
    private string? FormatAwayComet(AstrolabeTarget target, double forecastTotalDays, int daysPerYear)
    {
        if (target.Kind != AstrolabeTargetKind.Comet
            || !cometsById.TryGetValue(target.SourceId, out var comet))
        {
            return null;
        }

        var days = CometApparitionSchedule.DaysUntilVisible(comet, forecastTotalDays, daysPerYear);
        return days <= 0 ? null : $"away, returns in {FormatReturn(days, daysPerYear)}";
    }

    /// <summary>
    /// A wait measured in days for anything within a couple of years, and in years past that. A
    /// comet with a seventy-five year period is thousands of days out, and no player counts those.
    /// </summary>
    private static string FormatReturn(double days, int daysPerYear)
        => days >= daysPerYear * 2
            ? $"{days / daysPerYear:0.0} years"
            : $"{days:0} days";

    private bool IsReadingAstrolabe()
    {
        var stack = api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        var code = stack?.Collectible?.Code;
        var isHeldAstrolabe = string.Equals(code?.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
            && string.Equals(code?.Path, "calibrated-astrolabe", StringComparison.OrdinalIgnoreCase);
        return isHeldAstrolabe &&
               (AstrolabeReadingState.IsReading || api.World.Player.Entity.Controls.RightMouseDown);
    }

    private void RenderLines(string title, string reading, string clock, string help)
    {
        RenderLine(
            ref titleTexture,
            ref titleText,
            title,
            new CairoFont(20, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            TitleTopOffset);
        RenderLine(
            ref readingTexture,
            ref readingText,
            reading,
            new CairoFont(22, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            ReadingTopOffset);
        RenderLine(
            ref clockTexture,
            ref clockText,
            clock,
            new CairoFont(18, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            ClockTopOffset);
        RenderLine(
            ref helpTexture,
            ref helpText,
            help,
            new CairoFont(15, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
            HelpTopOffset);
    }

    private void RenderLine(
        ref LoadedTexture? texture,
        ref string? cachedText,
        string text,
        CairoFont font,
        float topOffset)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            DeleteTexture(texture);
            texture = null;
            cachedText = null;
            return;
        }

        if (texture is null || cachedText != text)
        {
            DeleteTexture(texture);
            cachedText = text;
            texture = api.Gui.TextTexture.GenTextTexture(text, font, null);
        }

        if (texture.TextureId == 0)
        {
            return;
        }

        var x = (api.Render.FrameWidth - texture.Width) / 2f;
        api.Render.Render2DLoadedTexture(texture, x, topOffset, 1001f);
    }

    private void DeleteTexture(LoadedTexture? texture)
    {
        if (texture is null)
        {
            return;
        }

        if (texture.TextureId != 0)
        {
            try
            {
                api.Render.GLDeleteTexture(texture.TextureId);
            }
            catch (Exception exception)
            {
                api.Logger.Warning("AstraTerra could not delete astrolabe HUD texture during cleanup: {0}", exception.Message);
            }
        }

        texture.TextureId = 0;
    }

    private static string FormatReading(AstrolabeReading reading)
    {
        var state = reading.MotionState switch
        {
            AstrolabeMotionState.Rising => "rising",
            AstrolabeMotionState.Culminating => "culminating",
            AstrolabeMotionState.Setting => "setting",
            AstrolabeMotionState.BelowHorizon => "below horizon",
            AstrolabeMotionState.NeverRises => "never rises",
            _ => "unknown"
        };
        var horizon = reading.HorizonClass == AstrolabeHorizonClass.Circumpolar
            ? ", circumpolar"
            : string.Empty;
        var direction = AstrolabeService.CompassPoint(reading.AzimuthDeg);
        var altitude = reading.AltitudeDeg.ToString("+0.0;-0.0;0.0");
        var transit = reading.MotionState == AstrolabeMotionState.NeverRises
            ? string.Empty
            : $", {FormatTransit(reading.HoursUntilTransit)}";
        return $"{direction}, {altitude}°, {state}{horizon}{transit}";
    }

    private static string FormatTransit(double hoursUntilTransit)
        => hoursUntilTransit < 0.05
            ? "transiting now"
            : $"transit in {hoursUntilTransit:0.0} h";

    private static string FormatForecastOffset(double forecastHours, double hoursPerDay)
    {
        if (forecastHours < 0.05)
        {
            return "Now";
        }

        var days = (int)Math.Floor(forecastHours / hoursPerDay);
        var hours = forecastHours - (days * hoursPerDay);
        if (days == 0)
        {
            return $"+{hours:0} h";
        }

        return hours < 0.05
            ? $"+{days} d"
            : $"+{days} d {hours:0} h";
    }

    private static int PositiveModulo(int value, int modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }
}
