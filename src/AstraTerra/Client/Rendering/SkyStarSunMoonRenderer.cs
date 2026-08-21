using System.Diagnostics;
using System.Reflection;
using AstraTerra.Astronomy;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using HarmonyLib;
using OpenTK.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AstraTerra.Client.Rendering;

[HarmonyPatch(typeof(SystemRenderSunMoon), nameof(SystemRenderSunMoon.OnRenderFrame3D))]
public static class SkyStarSunMoonRenderer
{
    /// <summary>
    /// Naked-eye sprites. The rays are scintillation — air, not optics — which is why they belong to
    /// the eye and not to the scoped view.
    /// </summary>
    public const string StarTexturePath = "astraterra:environment/star-rays-12-smooth";
    public const string FaintStarTexturePath = "astraterra:environment/star-dog-crisp";

    /// <summary>
    /// Scoped sprites. A telescope steadies the air out of the image, so the rays go: a star
    /// collapses to an Airy disc, a bright core inside one faint diffraction ring, and a fainter star
    /// to a plain point too dim to show a ring at all.
    /// </summary>
    public const string TelescopeStarTexturePath = "astraterra:environment/star-log-ring";
    public const string TelescopeFaintStarTexturePath = "astraterra:environment/star-derivative-cross";

    /// <summary>
    /// A planet is the one thing a telescope actually resolves. Stars stay points at any
    /// magnification because they are too far away to show a disc; a planet is near enough to open
    /// into one, which is the whole reason an observer bothers to point a scope at it.
    /// </summary>
    public const string TelescopePlanetTexturePath = "astraterra:environment/star-gaussian-soft";

    private const string ConstellationDotTexturePath = "astraterra:environment/star-pixel";
    private static readonly FieldInfo? QuadModelRefField = FindField("quadModelRef", "quadModel");
    private static readonly FieldInfo? ImageSizeField = typeof(SystemRenderSunMoon).GetField("ImageSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private const double MinimumSkyRenderDarkness = 0.10;
    private const float MinimumStarAlpha = 0.035f;
    private const float SkyDistance = 40.0f;
    private const float MeteorSkyDistance = 39.8f;
    private const float StarAngularSizePerPixelDeg = 0.06f;
    private const float StarGlowMaxAlpha = 0.35f;

    /// <summary>
    /// How much wider a planet's glow grows than its core, at full brilliance. Larger than the star
    /// glow: a planet outshining every star around it is the thing that makes a player look twice.
    /// </summary>
    private const float PlanetGlowSizeRange = 1.8f;
    private const float PlanetGlowMaxAlpha = 0.5f;
    private const float DeepSkyBrightnessScale = 0.42f;
    private const double ConstellationDotSpacingDeg = 0.65;
    private const float ConstellationDotAngularSizeDeg = 0.14f;
    private const double FaintStarMagnitudeThreshold = 2.5;

    /// <summary>How often the cost and draw summaries are written to the debug log while the sky is drawing.</summary>
    public const double SkyDrawLogIntervalSeconds = 30.0;

    private static ICoreClientAPI? api;
    private static AstraTerraConfig? config;
    private static StarCatalog? catalog;
    private static IReadOnlyList<MeteorShowerEntry> meteorShowers = Array.Empty<MeteorShowerEntry>();
    private static PlanetCatalog? planetCatalog;
    private static PlanetRenderModel? planetModel;
    private static int planetModelDaysPerYear;
    private static double planetModelHoursPerDay;
    private static MeteorShowerVisualModel meteorVisuals = new();
    private static readonly SkyPassMetrics Metrics = new();
    private static readonly RenderedStar[] NoStars = [];
    private static readonly RenderedPlanet[] NoPlanets = [];
    private static double secondsSinceLastSkipLog;
    private static bool starTextureLoadFailed;
    private static bool faintStarTextureLoadFailed;
    private static bool telescopeStarTextureLoadFailed;
    private static bool telescopeFaintStarTextureLoadFailed;
    private static bool telescopePlanetTextureLoadFailed;
    private static bool constellationDotTextureLoadFailed;
    private static bool forceDaylightStars;
    private static bool renderingDisabledAfterFailure;
    private static int starTextureId;
    private static int faintStarTextureId;
    private static int telescopeStarTextureId;
    private static int telescopeFaintStarTextureId;
    private static int telescopePlanetTextureId;
    private static int constellationDotTextureId;
    private static MeshRef? deepSkyQuadMesh;
    private static MeshRef? meteorStreakMesh;
    private static MeshRef? constellationDotMesh;

    // One mesh per sprite in play, rebuilt only when the projection is. There is no texture atlas
    // here and none is needed: the sky only ever draws two star sprites at a time (bright and faint,
    // or their scoped equivalents) plus one for the planets, so grouping by sprite collapses about
    // 3000 draw calls into three without touching the art.
    private static readonly List<SkyBillboard> BrightStarBillboards = new(6000);
    private static readonly List<SkyBillboard> FaintStarBillboards = new(6000);
    private static readonly List<SkyBillboard> PlanetBillboards = new(32);
    private static MeshRef? brightStarMesh;
    private static MeshRef? faintStarMesh;
    private static MeshRef? planetMesh;
    private static int brightStarMeshCapacity;
    private static int faintStarMeshCapacity;
    private static int planetMeshCapacity;
    private static float cachedStarMeshAngularScale = float.NaN;
    private static bool cachedStarMeshScoped;
    private static bool starMeshesDirty = true;
    private static int constellationDotMeshCapacity;
    private static int constellationDotMeshDrawnCount;
    private static float constellationDotMeshAngularSizeDeg = float.NaN;
    private static IReadOnlyList<SkyConstellationDot>? constellationDotMeshDots;

    // The sky pass runs on the render thread every frame it draws, over a catalog of five thousand
    // stars. Everything below exists so that pass allocates nothing and repeats no work: buffers are
    // reused between frames, and the projection is only redone when the sky has actually turned far
    // enough to see. Rebuilding it per frame cost over 10 ms and a gigabyte of garbage a minute,
    // which players saw as stutter, lag spikes and a client that would not give memory back.
    private static readonly List<RenderedStar> ProjectedStars = new(6000);
    private static readonly List<RenderedStar> DrawableStars = new(6000);
    private static readonly List<RenderedPlanet> DrawablePlanets = new(16);

    // Vec4f is a class in the Vintage Story API, so a tint built per body per frame is thousands of
    // heap objects a second. The shader uploads a uniform the moment it is assigned, so one mutable
    // instance can serve every draw.
    private static readonly Vec4f ScratchTint = new(1f, 1f, 1f, 1f);
    private static readonly Vec4f ScratchLight = new(1f, 1f, 1f, 1f);
    private static readonly Dictionary<int, RenderedStar> VisibleStarsByHip = new(6000);
    private static double cachedStarLatitudeDeg = double.NaN;
    private static double cachedStarSiderealDeg = double.NaN;
    private static double cachedStarBrightnessBias = double.NaN;
    private static string? cachedJournalJson;
    private static ConstellationJournal? cachedJournal;
    private static ConstellationJournal? cachedDotsJournal;
    private static IReadOnlyList<SkyConstellationDot>? cachedConstellationDots;

    /// <summary>
    /// How far the sky must turn, or the observer move, before the star projection is redone.
    /// </summary>
    /// <remarks>
    /// A star billboard is about half a degree across, so a twentieth of a degree is well inside one
    /// sprite -- there is nothing on screen to see. At Vintage Story's default time speed the sky
    /// turns about a quarter of a degree a second, so this refreshes a handful of times a second
    /// instead of sixty.
    /// </remarks>
    private const double StarRefreshThresholdDeg = 0.05;
    private static readonly Dictionary<string, int> DeepSkyTextureIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedDeepSkyTexturePaths = new(StringComparer.OrdinalIgnoreCase);

    public static bool ForceDaylightStars => forceDaylightStars;
    public static bool ShouldRenderVanillaStarfield
        => config is null || StarfieldModeParser.ShowsVanilla(config.GetStarfieldMode());

    public static void Initialize(
        ICoreClientAPI clientApi,
        AstraTerraConfig loadedConfig,
        StarCatalog loadedCatalog,
        IReadOnlyList<MeteorShowerEntry> loadedMeteorShowers,
        PlanetCatalog? loadedPlanets = null)
    {
        ArgumentNullException.ThrowIfNull(loadedMeteorShowers);
        Reset();
        api = clientApi;
        config = loadedConfig;
        catalog = loadedCatalog;
        meteorShowers = loadedMeteorShowers;
        planetCatalog = loadedPlanets;
        meteorVisuals = new MeteorShowerVisualModel((ulong)Environment.TickCount64);
        clientApi.Logger.Notification(
            "AstraTerra sky renderer initialized: renderPass=sunMoon3D; minSize={0:0.0}px; maxSize={1:0.0}px; angularScale={2:0.000}deg/px; meteorShowers={3}; planets={4}",
            StarBillboardSizing.MinimumCoreDiameterPixels,
            StarBillboardSizing.MaximumCoreDiameterPixels,
            StarAngularSizePerPixelDeg,
            meteorShowers.Count,
            planetCatalog?.Planets.Count ?? 0);
    }

    /// <summary>
    /// Builds the planet model on demand rather than at startup, because a client only learns the
    /// world's <c>daysPerYear</c> when the server hands it over, and that number sets how fast every
    /// orbit runs.
    /// </summary>
    private static PlanetRenderModel? ResolvePlanetModel(int daysPerYear, double hoursPerDay)
    {
        if (planetCatalog is null)
        {
            return null;
        }

        if (planetModel is null
            || daysPerYear != planetModelDaysPerYear
            || Math.Abs(hoursPerDay - planetModelHoursPerDay) > 1e-9)
        {
            planetModel = new PlanetRenderModel(planetCatalog, daysPerYear, hoursPerDay);
            planetModelDaysPerYear = daysPerYear;
            planetModelHoursPerDay = hoursPerDay;
        }

        return planetModel;
    }

    public static void Reset()
    {
        deepSkyQuadMesh?.Dispose();
        deepSkyQuadMesh = null;
        meteorStreakMesh?.Dispose();
        meteorStreakMesh = null;
        constellationDotMesh?.Dispose();
        constellationDotMesh = null;
        brightStarMesh?.Dispose();
        brightStarMesh = null;
        faintStarMesh?.Dispose();
        faintStarMesh = null;
        planetMesh?.Dispose();
        planetMesh = null;
        brightStarMeshCapacity = 0;
        faintStarMeshCapacity = 0;
        planetMeshCapacity = 0;
        cachedStarMeshAngularScale = float.NaN;
        starMeshesDirty = true;
        BrightStarBillboards.Clear();
        FaintStarBillboards.Clear();
        PlanetBillboards.Clear();
        constellationDotMeshCapacity = 0;
        constellationDotMeshDrawnCount = 0;
        constellationDotMeshAngularSizeDeg = float.NaN;
        constellationDotMeshDots = null;
        api = null;
        config = null;
        catalog = null;
        meteorShowers = Array.Empty<MeteorShowerEntry>();
        planetCatalog = null;
        planetModel = null;
        planetModelDaysPerYear = 0;
        planetModelHoursPerDay = 0;
        meteorVisuals.Clear();
        Metrics.Reset();
        SkyRenderPaths.Reset();
        ProjectedStars.Clear();
        DrawableStars.Clear();
        DrawablePlanets.Clear();
        VisibleStarsByHip.Clear();
        cachedStarLatitudeDeg = double.NaN;
        cachedStarSiderealDeg = double.NaN;
        cachedStarBrightnessBias = double.NaN;
        cachedJournalJson = null;
        cachedJournal = null;
        cachedDotsJournal = null;
        cachedConstellationDots = null;
        secondsSinceLastSkipLog = 0;
        starTextureLoadFailed = false;
        faintStarTextureLoadFailed = false;
        telescopeStarTextureLoadFailed = false;
        telescopeFaintStarTextureLoadFailed = false;
        telescopePlanetTextureLoadFailed = false;
        constellationDotTextureLoadFailed = false;
        forceDaylightStars = false;
        renderingDisabledAfterFailure = false;
        starTextureId = 0;
        faintStarTextureId = 0;
        telescopeStarTextureId = 0;
        telescopeFaintStarTextureId = 0;
        telescopePlanetTextureId = 0;
        constellationDotTextureId = 0;
        DeepSkyTextureIds.Clear();
        FailedDeepSkyTexturePaths.Clear();
    }

    public static string SetForceDaylightStars(bool enabled)
    {
        forceDaylightStars = enabled;
        api?.Logger.Notification("AstraTerra daylight star override: enabled={0}", forceDaylightStars);
        return forceDaylightStars
            ? "AstraTerra daylight star override enabled. Stars and constellation lines will render during daylight."
            : "AstraTerra daylight star override disabled. Stars now follow normal daylight visibility.";
    }

    public static bool ShouldRenderForDarkness(double naturalDarkness, bool forceDaylight)
    {
        return forceDaylight || naturalDarkness > MinimumSkyRenderDarkness;
    }

    public static void Postfix(SystemRenderSunMoon __instance, float dt)
    {
        if (renderingDisabledAfterFailure)
        {
            return;
        }

        try
        {
            var start = Stopwatch.GetTimestamp();
            Metrics.BeginFrame();
            var drew = PostfixCore(__instance, dt);
            if (drew)
            {
                Metrics.EndFrame(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            try
            {
                api?.Logger.Warning("AstraTerra disabled sky rendering after an unexpected error: {0}", exception);
            }
            catch
            {
                // Avoid surfacing cleanup/logging failures back into Vintage Story's render loop.
            }
        }
    }

    /// <summary>Draws the sky, and reports whether it drew anything worth timing.</summary>
    private static bool PostfixCore(SystemRenderSunMoon __instance, float dt)
    {
        if (api is null || config is null || catalog is null)
        {
            return false;
        }

        if (!StarfieldModeParser.ShowsAstraTerra(config.GetStarfieldMode()))
        {
            meteorVisuals.Clear();
            return false;
        }

        var calendar = api.World.Calendar;
        var naturalDarkness = 1.0 - calendar.DayLightStrength;
        var renderDarkness = forceDaylightStars ? 1.0 : naturalDarkness;
        if (!ShouldRenderForDarkness(naturalDarkness, forceDaylightStars))
        {
            meteorVisuals.Clear();
            LogSkyStep(dt, "skipped daylight: daylight={0:0.00}; darkness={1:0.00}", calendar.DayLightStrength, naturalDarkness);
            return false;
        }

        // No sky-exposure gate here, and none belongs here. This pass draws inside the sun/moon
        // render (Opaque 0.3), before opaque terrain (0.37), so terrain painted afterwards occludes
        // the stars per pixel -- which is precisely how vanilla's own starfield works:
        // SystemRenderNightSky draws at Opaque 0.1 with the depth test off and no exposure check of
        // any kind, and is still not visible from inside a cave.
        //
        // A whole-sky gate cannot express "sky visible in that direction", so it can only ever be
        // wrong in one direction or the other: a rain-map check hid the sky under any tree, and a
        // light-level check hid it from a player standing indoors looking out of a window. The
        // renderers that genuinely cannot be occluded -- the grid, the overlay, the sextant readout,
        // all of which draw after terrain -- ask SkyExposure instead.
        var playerPos = api.World.Player.Entity.Pos;

        var latitude = LatitudeMapper.MapGameLatitude(playerPos.Z, calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(playerPos.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);
        var brightnessBias = config.StarBrightnessBias * (float)renderDarkness;
        // The projection feeds both the stars and the constellation lines, so it is skipped only when
        // neither will be drawn. That is what makes `.stars render stars off` plus
        // `.stars render constellations off` measure the projection's cost and not just its draw.
        var starsNeeded = SkyRenderPaths.IsEnabled(SkyRenderPath.Stars) || SkyRenderPaths.IsEnabled(SkyRenderPath.Constellations);
        var starsRefreshed = starsNeeded
            ? EnsureProjectedStars(catalog.Stars, latitude, localSiderealAngle, brightnessBias)
            : ClearProjectedStars();
        var visibleStars = ProjectedStars;
        var drawableStars = DrawableStars;
        var solarLongitude = CelestialMath.GetSolarLongitudeDegrees(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear));
        var meteorReadings = MeteorShowerActivity.ReadAll(
            meteorShowers,
            solarLongitude,
            latitude,
            localSiderealAngle,
            naturalDarkness,
            calendar.MoonPhaseBrightness);
        var meteorStreaks = meteorVisuals.Advance(
            dt,
            meteorReadings,
            config.GetDebugMeteorRateMultiplier());
        var visiblePlanets = ResolvePlanetModel(Math.Max(1, calendar.DaysPerYear), Math.Max(1.0, calendar.HoursPerDay))
            ?.ProjectVisiblePlanets(calendar.TotalDays, latitude, localSiderealAngle, brightnessBias)
            ?? (IReadOnlyList<RenderedPlanet>)Array.Empty<RenderedPlanet>();
        var drawablePlanets = FilterDrawablePlanets(visiblePlanets);
        // The scope's deep-sky plates are the one thing that can still be worth drawing with no star
        // above the horizon, so they keep the pass alive.
        if (drawableStars.Count == 0 && drawablePlanets.Count == 0 && meteorStreaks.Count == 0 && !TelescopeScopeState.IsScoped)
        {
            LogSkyStep(
                dt,
                "skipped no drawable sky objects: catalog={0}; visibleStars={1}; meteorShowers={2}; latitude={3:0.0}; localSidereal={4:0.0}; brightnessBias={5:0.00}",
                catalog.Stars.Count,
                visibleStars.Count,
                meteorShowers.Count,
                latitude,
                localSiderealAngle,
                brightnessBias);
            return false;
        }

        // Reading the book means deserializing its journal, which is far too much work to repeat
        // sixty times a second for a page that has not changed. The written JSON is the key.
        var journal = ReadJournalCached(api.World.Player.Entity.LeftHandItemSlot?.Itemstack);
        IReadOnlyList<SkyConstellationDot> constellationDots = Array.Empty<SkyConstellationDot>();
        if (journal is not null)
        {
            if (starsRefreshed || cachedConstellationDots is null || !ReferenceEquals(journal, cachedDotsJournal))
            {
                VisibleStarsByHip.Clear();
                foreach (var star in drawableStars)
                {
                    VisibleStarsByHip[star.Hip] = star;
                }

                var constellationSegments = ConstellationRenderModel.BuildConstellationSegments(journal.Constellations, VisibleStarsByHip);
                cachedConstellationDots = ConstellationRenderModel.BuildSkyDots(constellationSegments, ConstellationDotSpacingDeg);
                cachedDotsJournal = journal;
            }

            constellationDots = cachedConstellationDots;
        }

        EnsureStarTextures(api);
        if (starTextureId == 0 || faintStarTextureId == 0 || telescopeStarTextureId == 0 || telescopeFaintStarTextureId == 0)
        {
            LogSkyStep(
                dt,
                "skipped missing star texture: main={0}/{1}; faint={2}/{3}; telescopeMain={4}/{5}; telescopeFaint={6}/{7}",
                starTextureId,
                starTextureLoadFailed,
                faintStarTextureId,
                faintStarTextureLoadFailed,
                telescopeStarTextureId,
                telescopeStarTextureLoadFailed,
                telescopeFaintStarTextureId,
                telescopeFaintStarTextureLoadFailed);
            return false;
        }

        EnsureConstellationDotTexture(api);

        IReadOnlyList<RenderedDeepSkyObject> visibleDeepSkyObjects = TelescopeScopeState.IsScoped
            ? DeepSkyRenderModel.ProjectVisibleObjects(catalog.DeepSkyObjects, latitude, localSiderealAngle, brightnessBias)
            : Array.Empty<RenderedDeepSkyObject>();

        var quadModel = QuadModelRefField?.GetValue(__instance) as MeshRef;
        var imageSize = ImageSizeField?.GetValue(__instance) as int? ?? 256;
        if (quadModel is null)
        {
            LogSkyStep(dt, "skipped missing sun/moon quad mesh: quadField={0}; imageSize={1}", QuadModelRefField?.Name ?? "<missing>", imageSize);
            return false;
        }

        RenderSky(
            api,
            drawableStars,
            drawablePlanets,
            visibleDeepSkyObjects,
            constellationDots,
            meteorStreaks,
            FindStrongestReading(meteorReadings),
            quadModel,
            imageSize,
            dt,
            calendar.DayLightStrength,
            playerPos.Yaw,
            playerPos.Pitch,
            forceDaylightStars);

        return true;
    }

    private static void RenderSky(
        ICoreClientAPI clientApi,
        IReadOnlyList<RenderedStar> visibleStars,
        IReadOnlyList<RenderedPlanet> visiblePlanets,
        IReadOnlyList<RenderedDeepSkyObject> visibleDeepSkyObjects,
        IReadOnlyList<SkyConstellationDot> constellationDots,
        IReadOnlyList<RenderedMeteorStreak> meteorStreaks,
        MeteorShowerReading? strongestMeteorReading,
        MeshRef quadModel,
        int imageSize,
        float deltaTime,
        float daylight,
        double yaw,
        double pitch,
        bool forceDaylight)
    {
        var render = clientApi.Render;
        var shader = render.StandardShader;
        var useTelescopeSprites = TelescopeScopeState.IsScoped;
        var brightStarTextureId = useTelescopeSprites ? telescopeStarTextureId : starTextureId;
        var dimStarTextureId = useTelescopeSprites ? telescopeFaintStarTextureId : faintStarTextureId;

        // Naked-eye planets share the star sprite: at roughly eight screen pixels no sprite's shape
        // survives, and tint plus glow carry the difference. Under the scope the billboard is
        // magnified up to sixteen times, the shape becomes the whole picture, and a planet gets its
        // own resolved disc. Falls back to the star sprite rather than dropping the planet.
        var planetTextureId = useTelescopeSprites && telescopePlanetTextureId != 0
            ? telescopePlanetTextureId
            : brightStarTextureId;

        // Billboards are sized in degrees, so magnification would otherwise enlarge them. A star has
        // to shrink back to a point under the scope, both because that is what an eyepiece shows and
        // because the deep-sky plates it is drawn over carry their own stars at 0.002 to 0.03 deg.
        var fovMultiplier = useTelescopeSprites ? TelescopeScopeState.GetFovMultiplier() : 1.0f;
        var starAngularScale = StarBillboardSizing.CalculateStarAngularScale(useTelescopeSprites, fovMultiplier);
        var planetAngularScale = StarBillboardSizing.CalculatePlanetAngularScale(useTelescopeSprites, fovMultiplier);
        var drawnCount = 0;
        var smallestDrawnSize = double.MaxValue;
        var largestDrawnSize = 0.0;
        var modelMatrixBuffer = new float[16];

        UpdateMeteorStreakMesh(clientApi, meteorStreaks);

        render.GlToggleBlend(true, EnumBlendMode.Glow);
        render.GlDisableCullFace();
        render.GLDisableDepthTest();
        render.GLDepthMask(false);

        try
        {
            shader.Use();
            shader.Uniform("skyShaded", 0);
            shader.RgbaAmbientIn = ColorUtil.WhiteRgbVec;
            shader.RgbaFogIn = render.FogColor;
            shader.ExtraGlow = 0;
            shader.FogMinIn = render.FogMin;
            shader.FogDensityIn = render.FogDensity;
            shader.DontWarpVertices = 0;
            shader.AddRenderFlags = 0;
            shader.ExtraZOffset = 0f;
            shader.NormalShaded = 0;
            shader.OverlayOpacity = 0f;
            shader.ExtraGodray = 1f / 3f;
            shader.AlphaTest = 0.01f;
            shader.ViewMatrix = render.CameraMatrixOriginf;
            shader.ProjectionMatrix = render.CurrentProjectionMatrix;

            // Each path is switched independently so a player can answer "which one costs me?" in
            // one session, without a rebuild. Everything else keeps drawing.
            if (SkyRenderPaths.IsEnabled(SkyRenderPath.Stars))
            {
                EnsureStarMeshes(clientApi, visibleStars, visiblePlanets, starAngularScale, planetAngularScale, useTelescopeSprites);

                // Additive blending makes draw order between these irrelevant, which is what lets a
                // star's glow and its core sit in different meshes.
                drawnCount += DrawBillboardBatch(clientApi, shader, brightStarMesh, BrightStarBillboards.Count, brightStarTextureId, modelMatrixBuffer);
                drawnCount += DrawBillboardBatch(clientApi, shader, faintStarMesh, FaintStarBillboards.Count, dimStarTextureId, modelMatrixBuffer);
                drawnCount += DrawBillboardBatch(clientApi, shader, planetMesh, PlanetBillboards.Count, planetTextureId, modelMatrixBuffer);

                for (var index = 0; index < visibleStars.Count; index++)
                {
                    var size = StarBillboardSizing.CalculateCoreDiameterPixels(visibleStars[index].Size);
                    smallestDrawnSize = Math.Min(smallestDrawnSize, size);
                    largestDrawnSize = Math.Max(largestDrawnSize, size);
                }
            }

            if (visibleDeepSkyObjects.Count > 0 && SkyRenderPaths.IsEnabled(SkyRenderPath.DeepSky))
            {
                // The telescope photographs are foreground plates. Standard alpha
                // blending lets their dark sky attenuate the catalog sprites below;
                // additive glow blending would make draw order visually irrelevant.
                render.GlToggleBlend(true, EnumBlendMode.Standard);
                foreach (var deepSkyObject in visibleDeepSkyObjects)
                {
                    var textureId = ResolveDeepSkyTexture(clientApi, deepSkyObject);
                    if (textureId != 0)
                    {
                        RenderDeepSkyQuad(clientApi, shader, deepSkyObject, textureId, modelMatrixBuffer);
                    }
                }

                render.GlToggleBlend(true, EnumBlendMode.Glow);
            }

            if (constellationDotTextureId != 0 && constellationDots.Count > 0 && SkyRenderPaths.IsEnabled(SkyRenderPath.Constellations))
            {
                // One mesh, one draw call. The dots are overlay marks, not sky objects: they follow
                // the star scale so a line stays a fine trail at any magnification instead of
                // swelling into blobs, which is why the scale is part of the mesh's rebuild key.
                EnsureConstellationDotMesh(clientApi, constellationDots, starAngularScale);
                RenderConstellationDots(clientApi, shader, modelMatrixBuffer);
            }

            if (meteorStreakMesh is not null && meteorStreaks.Count > 0 && constellationDotTextureId != 0 && SkyRenderPaths.IsEnabled(SkyRenderPath.Meteors))
            {
                RenderMeteorStreaks(clientApi, shader, modelMatrixBuffer);
            }
        }
        finally
        {
            shader.Stop();
            render.GLDepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Glow);
            render.GlEnableCullFace();
            render.GLEnableDepthTest();
        }

        if (Metrics.TryTakeReport(deltaTime, SkyDrawLogIntervalSeconds, out var report))
        {
            // Cost first, because that is the question anyone reading this log is asking. Draw calls
            // and uploads are counted at the GL call, so they stay honest as paths are batched.
            clientApi.Logger.VerboseDebug(
                "AstraTerra sky cost: frames={0}; ms/frame={1:0.00} (peak {2:0.00}); drawCalls/frame={3:0} (peak {4}); meshUploads={5}; meshUpdates={6}; paths={7}",
                report.Frames,
                report.AverageMilliseconds,
                report.PeakMilliseconds,
                report.AverageDrawCalls,
                report.PeakDrawCalls,
                report.MeshUploads,
                report.MeshUpdates,
                SkyRenderPaths.Describe());
            clientApi.Logger.VerboseDebug(
                "AstraTerra sky draw: pass=sunMoon3D; visible={0}; planets={7}; sprites={1}; constellationDots={2} (batched={8}/{9} in 1 draw); daylight={3:0.00}; forceDaylight={4}; azimuth={5:0.0}; altitude={6:0.0}",
                visibleStars.Count,
                drawnCount,
                constellationDots.Count,
                daylight,
                forceDaylight,
                CelestialMath.NormalizeDegrees(ToDegrees(yaw)),
                Math.Clamp(-ToDegrees(pitch), -89.0, 89.0),
                visiblePlanets.Count,
                constellationDotMeshDrawnCount,
                constellationDotMeshCapacity);
            if (visiblePlanets.Count > 0)
            {
                clientApi.Logger.VerboseDebug(
                    "AstraTerra planet draw: {0}",
                    string.Join(
                        "; ",
                        visiblePlanets.Select(planet =>
                            $"{planet.DisplayName} mag={planet.VisualMagnitude:0.0} alt={planet.AltitudeDeg:0.0} az={planet.AzimuthDeg:0.0}")));
            }
            if (visibleDeepSkyObjects.Count > 0)
            {
                clientApi.Logger.VerboseDebug(
                    "AstraTerra telescope deep-sky draw: visible={0}",
                    visibleDeepSkyObjects.Count);
            }
            clientApi.Logger.VerboseDebug(
                "AstraTerra sky size: minConfigured={0:0.0}px; scale={1:0.0}; maxConfigured={2:0.0}px; drawnSizeRange={3:0.0}-{4:0.0}px; telescopeSprites={5}",
                StarBillboardSizing.MinimumCoreDiameterPixels,
                StarBillboardSizing.ProjectedSizeScale,
                StarBillboardSizing.MaximumCoreDiameterPixels,
                drawnCount == 0 ? 0 : smallestDrawnSize,
                largestDrawnSize,
                useTelescopeSprites);
            if (strongestMeteorReading is not null || meteorStreaks.Count > 0)
            {
                clientApi.Logger.VerboseDebug(
                    "AstraTerra meteor shower: strongest={0}; rate={1:0.0}/h; radiantAz={2:0.0}; radiantAlt={3:0.0}; activeStreaks={4}",
                    strongestMeteorReading?.Shower.DisplayName ?? "none",
                    strongestMeteorReading?.ObservedHourlyRate ?? 0.0,
                    strongestMeteorReading?.AzimuthDeg ?? 0.0,
                    strongestMeteorReading?.AltitudeDeg ?? 0.0,
                    meteorStreaks.Count);
            }
        }
    }

    /// <summary>
    /// Rebuilds the star and planet batches when the projection behind them has moved, or when the
    /// scope changes how large a sprite should be.
    /// </summary>
    /// <remarks>
    /// Colour rides on the vertices here, where it used to be a per-star pair of uniforms. That is
    /// what allows one draw call per sprite, and it is also a small change in how a star is shaded:
    /// the old path passed the tint as both <c>RgbaTint</c> and <c>RgbaLightIn</c>, so the shader
    /// applied it twice, once directly and once through its light mix. The batch applies it once.
    /// Star colours therefore read slightly more saturated than before — a look change to be judged
    /// by eye, not a correctness one.
    /// </remarks>
    private static void EnsureStarMeshes(
        ICoreClientAPI clientApi,
        IReadOnlyList<RenderedStar> visibleStars,
        IReadOnlyList<RenderedPlanet> visiblePlanets,
        float starAngularScale,
        float planetAngularScale,
        bool useTelescopeSprites)
    {
        if (!starMeshesDirty &&
            Math.Abs(starAngularScale - cachedStarMeshAngularScale) < 1e-6f &&
            useTelescopeSprites == cachedStarMeshScoped)
        {
            return;
        }

        BrightStarBillboards.Clear();
        FaintStarBillboards.Clear();
        PlanetBillboards.Clear();

        for (var index = 0; index < visibleStars.Count; index++)
        {
            var star = visibleStars[index];
            var isFaint = star.VisualMagnitude > FaintStarMagnitudeThreshold;
            var sizePixels = StarBillboardSizing.CalculateCoreDiameterPixels(star.Size);
            var alpha = (float)Math.Clamp(star.Brightness, 0.0, 1.0);
            var (red, green, blue) = ColorFromTemperature(star.ColorTemperatureK);
            var target = isFaint ? FaintStarBillboards : BrightStarBillboards;

            var outerAlpha = StarGlowMaxAlpha * alpha * alpha;
            if (!isFaint && outerAlpha > 0.005f)
            {
                var glowSize = StarBillboardSizing.CalculateGlowDiameterPixels(sizePixels, alpha);
                BrightStarBillboards.Add(Billboard(star.Body, glowSize, starAngularScale, red, green, blue, outerAlpha));
            }

            target.Add(Billboard(star.Body, sizePixels, starAngularScale, red, green, blue, alpha));
        }

        // Planets take the same billboard path as stars, never the faint sprite, and get a glow
        // scaled by brilliance rather than by the saturated star curve, so that Venus reads as the
        // brightest thing in the sky and Saturn does not.
        for (var index = 0; index < visiblePlanets.Count; index++)
        {
            var planet = visiblePlanets[index];
            var alpha = (float)Math.Clamp(planet.Brightness, 0.0, 1.0);
            var brilliance = (float)Math.Clamp(planet.Brilliance, 0.0, 1.0);
            var sizePixels = StarBillboardSizing.CalculateCoreDiameterPixels(planet.Size);

            var glowAlpha = PlanetGlowMaxAlpha * alpha * brilliance;
            if (glowAlpha > 0.005f)
            {
                var glowSize = sizePixels * (1f + (PlanetGlowSizeRange * brilliance));
                PlanetBillboards.Add(Billboard(planet.Body, glowSize, planetAngularScale, planet.TintR, planet.TintG, planet.TintB, glowAlpha));
            }

            PlanetBillboards.Add(Billboard(planet.Body, sizePixels, planetAngularScale, planet.TintR, planet.TintG, planet.TintB, alpha));
        }

        UploadBillboardBatch(clientApi, BrightStarBillboards, ref brightStarMesh, ref brightStarMeshCapacity);
        UploadBillboardBatch(clientApi, FaintStarBillboards, ref faintStarMesh, ref faintStarMeshCapacity);
        UploadBillboardBatch(clientApi, PlanetBillboards, ref planetMesh, ref planetMeshCapacity);

        starMeshesDirty = false;
        cachedStarMeshAngularScale = starAngularScale;
        cachedStarMeshScoped = useTelescopeSprites;
    }

    private static SkyBillboard Billboard(
        RenderedBody body,
        float sizePixels,
        float angularScale,
        float red,
        float green,
        float blue,
        float alpha)
    {
        var angularSizeDeg = Math.Clamp(sizePixels * StarAngularSizePerPixelDeg * angularScale, 0.001f, 2.0f);
        return new SkyBillboard(
            body.DirectionX,
            body.DirectionY,
            body.DirectionZ,
            angularSizeDeg,
            ColorUtil.ColorFromRgba(ToColorByte(red), ToColorByte(green), ToColorByte(blue), ToColorByte(alpha)));
    }

    private static void UploadBillboardBatch(
        ICoreClientAPI clientApi,
        IReadOnlyList<SkyBillboard> billboards,
        ref MeshRef? mesh,
        ref int capacity)
    {
        if (billboards.Count == 0)
        {
            return;
        }

        var required = SkyBillboardMeshBuilder.GetCapacityFor(billboards.Count);
        var meshData = SkyBillboardMeshBuilder.Build(billboards, SkyDistance, Math.Max(required, capacity));
        if (mesh is null || required > capacity)
        {
            mesh?.Dispose();
            mesh = UploadMesh(clientApi, meshData);
            capacity = Math.Max(required, capacity);
        }
        else
        {
            UpdateMesh(clientApi, mesh, meshData);
        }
    }

    /// <summary>Draws one batch, and reports how many sprites it carried.</summary>
    private static int DrawBillboardBatch(
        ICoreClientAPI clientApi,
        IStandardShaderProgram shader,
        MeshRef? mesh,
        int count,
        int textureId,
        float[] modelMatrixBuffer)
    {
        if (mesh is null || count == 0 || textureId == 0)
        {
            return 0;
        }

        var entity = clientApi.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - clientApi.World.SeaLevel) / 10000f;
        var modelMatrix = Matrix4.CreateTranslation(0f, verticalOrigin, 0f);

        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = ColorUtil.WhiteArgbVec;
        shader.RgbaLightIn = ColorUtil.WhiteArgbVec;
        shader.Tex2D = textureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        DrawMesh(clientApi, mesh);
        ((IShaderProgram)shader).Uniform("skyShaded", 0);

        return count;
    }

    private static int ToColorByte(float channel) => (int)Math.Round(Math.Clamp(channel, 0f, 1f) * 255f);

    /// <summary>
    /// Rebuilds the batched dot mesh when the dots themselves change, or when the scope changes how
    /// large a dot should be.
    /// </summary>
    private static void EnsureConstellationDotMesh(
        ICoreClientAPI clientApi,
        IReadOnlyList<SkyConstellationDot> dots,
        float angularScale)
    {
        var angularSizeDeg = ConstellationDotAngularSizeDeg * angularScale;
        if (constellationDotMesh is not null &&
            ReferenceEquals(dots, constellationDotMeshDots) &&
            Math.Abs(angularSizeDeg - constellationDotMeshAngularSizeDeg) < 1e-6f)
        {
            return;
        }

        var capacity = ConstellationDotMeshBuilder.GetCapacityFor(dots.Count);
        var meshData = ConstellationDotMeshBuilder.Build(dots, SkyDistance, angularSizeDeg, capacity);

        // Vintage Story sizes a mesh's buffers on the first upload and never grows them, so a batch
        // that has outgrown its allocation needs a fresh mesh rather than an update into the old one.
        if (constellationDotMesh is null || capacity > constellationDotMeshCapacity)
        {
            constellationDotMesh?.Dispose();
            constellationDotMesh = UploadMesh(clientApi, meshData);
            constellationDotMeshCapacity = capacity;
        }
        else
        {
            UpdateMesh(clientApi, constellationDotMesh, meshData);
        }

        constellationDotMeshDots = dots;
        constellationDotMeshAngularSizeDeg = angularSizeDeg;
        constellationDotMeshDrawnCount = Math.Min(dots.Count, capacity);
    }

    private static void RenderConstellationDots(
        ICoreClientAPI clientApi,
        IStandardShaderProgram shader,
        float[] modelMatrixBuffer)
    {
        if (constellationDotMesh is null)
        {
            return;
        }

        var entity = clientApi.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - clientApi.World.SeaLevel) / 10000f;
        var modelMatrix = Matrix4.CreateTranslation(0f, verticalOrigin, 0f);

        // The tint that used to be a per-dot uniform now rides on the vertices.
        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = ColorUtil.WhiteArgbVec;
        shader.RgbaLightIn = ColorUtil.WhiteArgbVec;
        shader.Tex2D = constellationDotTextureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        DrawMesh(clientApi, constellationDotMesh);
        ((IShaderProgram)shader).Uniform("skyShaded", 0);
    }

    private static void UpdateMeteorStreakMesh(
        ICoreClientAPI clientApi,
        IReadOnlyList<RenderedMeteorStreak> meteorStreaks)
    {
        if (meteorStreaks.Count == 0)
        {
            return;
        }

        var meshData = MeteorStreakMeshBuilder.Build(meteorStreaks, MeteorSkyDistance);
        if (meteorStreakMesh is null)
        {
            meteorStreakMesh = UploadMesh(clientApi, meshData);
        }
        else
        {
            UpdateMesh(clientApi, meteorStreakMesh, meshData);
        }
    }

    private static void RenderMeteorStreaks(
        ICoreClientAPI clientApi,
        IStandardShaderProgram shader,
        float[] modelMatrixBuffer)
    {
        if (meteorStreakMesh is null)
        {
            return;
        }

        var entity = clientApi.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - clientApi.World.SeaLevel) / 10000f;
        var modelMatrix = Matrix4.CreateTranslation(0f, verticalOrigin, 0f);

        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = ColorUtil.WhiteArgbVec;
        shader.RgbaLightIn = ColorUtil.WhiteArgbVec;
        shader.Tex2D = constellationDotTextureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        DrawMesh(clientApi, meteorStreakMesh);
        ((IShaderProgram)shader).Uniform("skyShaded", 0);
    }

    private static void RenderDeepSkyQuad(
        ICoreClientAPI clientApi,
        IStandardShaderProgram shader,
        RenderedDeepSkyObject deepSkyObject,
        int textureId,
        float[] modelMatrixBuffer)
    {
        var meshData = DeepSkyQuadMeshBuilder.Build(deepSkyObject.QuadCorners, SkyDistance);
        if (deepSkyQuadMesh is null)
        {
            deepSkyQuadMesh = UploadMesh(clientApi, meshData);
        }
        else
        {
            UpdateMesh(clientApi, deepSkyQuadMesh, meshData);
        }

        var entity = clientApi.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - clientApi.World.SeaLevel) / 10000f;
        var modelMatrix = Matrix4.CreateTranslation(0f, verticalOrigin, 0f);
        var alpha = (float)Math.Clamp(deepSkyObject.Brightness * DeepSkyBrightnessScale, 0.0, 0.7);
        var tint = Set(
            ScratchTint,
            Math.Clamp(deepSkyObject.TintR, 0.0f, 1.0f),
            Math.Clamp(deepSkyObject.TintG, 0.0f, 1.0f),
            Math.Clamp(deepSkyObject.TintB, 0.0f, 1.0f),
            alpha);

        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = tint;
        shader.RgbaLightIn = Set(ScratchLight, tint.R, tint.G, tint.B, 1f);
        shader.Tex2D = textureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        DrawMesh(clientApi, deepSkyQuadMesh);
        ((IShaderProgram)shader).Uniform("skyShaded", 0);
    }

    private static int ResolveDeepSkyTexture(ICoreClientAPI clientApi, RenderedDeepSkyObject deepSkyObject)
    {
        foreach (var texturePath in deepSkyObject.TexturePaths)
        {
            if (TryGetDeepSkyTexture(clientApi, texturePath, out var textureId))
            {
                return textureId;
            }
        }

        return 0;
    }

    private static bool TryGetDeepSkyTexture(ICoreClientAPI clientApi, string texturePath, out int textureId)
    {
        textureId = 0;
        if (DeepSkyTextureIds.TryGetValue(texturePath, out textureId))
        {
            return textureId != 0;
        }

        if (FailedDeepSkyTexturePaths.Contains(texturePath))
        {
            return false;
        }

        try
        {
            foreach (var candidate in ExpandTextureCandidates(texturePath))
            {
                textureId = clientApi.Render.GetOrLoadTexture(new AssetLocation(candidate));
                if (textureId != 0)
                {
                    DeepSkyTextureIds[texturePath] = textureId;
                    clientApi.Logger.Notification("AstraTerra deep-sky texture resolved: configured={0}; loaded={1}; textureId={2}", texturePath, candidate, textureId);
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            clientApi.Logger.Error("AstraTerra could not load deep-sky texture {0}: {1}", texturePath, exception);
        }

        FailedDeepSkyTexturePaths.Add(texturePath);
        return false;
    }

    private static FieldInfo? FindField(params string[] names)
    {
        foreach (var name in names)
        {
            var field = typeof(SystemRenderSunMoon).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field is not null)
            {
                return field;
            }
        }

        return null;
    }

    private static IReadOnlyList<RenderedPlanet> FilterDrawablePlanets(IReadOnlyList<RenderedPlanet> visiblePlanets)
    {
        var drawablePlanets = DrawablePlanets;
        drawablePlanets.Clear();
        foreach (var planet in visiblePlanets)
        {
            if (planet.Brightness >= MinimumStarAlpha)
            {
                drawablePlanets.Add(planet);
            }
        }

        return drawablePlanets;
    }

    /// <summary>
    /// Refreshes the projected star buffers when the sky has turned or the observer has moved far
    /// enough to matter, and reports whether it did.
    /// </summary>
    private static bool EnsureProjectedStars(
        IReadOnlyList<StarCatalogEntry> stars,
        double latitudeDeg,
        double localSiderealDeg,
        double brightnessBias)
    {
        if (ProjectedStars.Count > 0 &&
            Math.Abs(latitudeDeg - cachedStarLatitudeDeg) < StarRefreshThresholdDeg &&
            Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(cachedStarSiderealDeg, localSiderealDeg)) < StarRefreshThresholdDeg &&
            Math.Abs(brightnessBias - cachedStarBrightnessBias) < 0.005)
        {
            return false;
        }

        StarRenderModel.ProjectVisibleStars(stars, latitudeDeg, localSiderealDeg, brightnessBias, ProjectedStars);
        DrawableStars.Clear();
        foreach (var star in ProjectedStars)
        {
            if (star.Brightness >= MinimumStarAlpha)
            {
                DrawableStars.Add(star);
            }
        }

        cachedStarLatitudeDeg = latitudeDeg;
        cachedStarSiderealDeg = localSiderealDeg;
        cachedStarBrightnessBias = brightnessBias;
        starMeshesDirty = true;
        return true;
    }

    /// <summary>Drops the projected sky when no path needs it, so a switched-off star path costs nothing.</summary>
    private static bool ClearProjectedStars()
    {
        if (ProjectedStars.Count == 0 && DrawableStars.Count == 0)
        {
            return false;
        }

        ProjectedStars.Clear();
        DrawableStars.Clear();
        starMeshesDirty = true;
        cachedStarLatitudeDeg = double.NaN;
        cachedStarSiderealDeg = double.NaN;
        cachedStarBrightnessBias = double.NaN;
        return true;
    }

    private static ConstellationJournal? ReadJournalCached(ItemStack? stack)
    {
        var json = stack?.Attributes?.GetString(ConstellationBookService.JournalJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            cachedJournalJson = null;
            cachedJournal = null;
            return null;
        }

        if (!string.Equals(json, cachedJournalJson, StringComparison.Ordinal))
        {
            cachedJournalJson = json;
            cachedJournal = ConstellationBookService.ReadJournal(stack);
        }

        return cachedJournal;
    }

    private static MeteorShowerReading? FindStrongestReading(IReadOnlyList<MeteorShowerReading> readings)
    {
        for (var index = 0; index < readings.Count; index++)
        {
            if (readings[index].ObservedHourlyRate > 0.0)
            {
                return readings[index];
            }
        }

        return null;
    }

    private static void EnsureStarTextures(ICoreClientAPI clientApi)
    {
        EnsureStarTexture(clientApi, StarTexturePath, ref starTextureId, ref starTextureLoadFailed);
        EnsureStarTexture(clientApi, FaintStarTexturePath, ref faintStarTextureId, ref faintStarTextureLoadFailed);
        EnsureStarTexture(clientApi, TelescopeStarTexturePath, ref telescopeStarTextureId, ref telescopeStarTextureLoadFailed);
        EnsureStarTexture(clientApi, TelescopeFaintStarTexturePath, ref telescopeFaintStarTextureId, ref telescopeFaintStarTextureLoadFailed);
        EnsureStarTexture(clientApi, TelescopePlanetTexturePath, ref telescopePlanetTextureId, ref telescopePlanetTextureLoadFailed);
    }

    private static void EnsureConstellationDotTexture(ICoreClientAPI clientApi)
    {
        EnsureStarTexture(clientApi, ConstellationDotTexturePath, ref constellationDotTextureId, ref constellationDotTextureLoadFailed);
    }

    private static void EnsureStarTexture(ICoreClientAPI clientApi, string texturePath, ref int textureId, ref bool loadFailed)
    {
        if (textureId != 0 || loadFailed)
        {
            return;
        }

        try
        {
            foreach (var candidate in ExpandTextureCandidates(texturePath))
            {
                textureId = clientApi.Render.GetOrLoadTexture(new AssetLocation(candidate));
                if (textureId != 0)
                {
                    clientApi.Logger.Notification("AstraTerra star texture resolved: configured={0}; loaded={1}; textureId={2}", texturePath, candidate, textureId);
                    return;
                }
            }
        }
        catch (Exception exception)
        {
            loadFailed = true;
            clientApi.Logger.Error("AstraTerra could not load star texture {0}: {1}", texturePath, exception);
        }
    }

    private static IEnumerable<string> ExpandTextureCandidates(string path)
    {
        var loadPath = ResolveTextureLoadPath(path);
        yield return loadPath;
        if (!string.Equals(loadPath, path, StringComparison.OrdinalIgnoreCase))
        {
            yield return path;
        }

        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            var withoutExtension = path[..^4];
            if (!string.Equals(withoutExtension, loadPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return withoutExtension;
            }
        }
    }

    private static string ResolveTextureLoadPath(string path)
    {
        var domainSeparatorIndex = path.IndexOf(':', StringComparison.Ordinal);
        if (domainSeparatorIndex < 0)
        {
            return path;
        }

        var domain = path[..domainSeparatorIndex];
        var rest = path[(domainSeparatorIndex + 1)..];
        if (!rest.StartsWith("textures/", StringComparison.OrdinalIgnoreCase))
        {
            rest = "textures/" + rest;
        }

        if (!rest.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            rest += ".png";
        }

        return domain + ":" + rest;
    }

    private static (float Red, float Green, float Blue) ColorFromTemperature(double temperatureKelvin)
    {
        var kelvin = Math.Clamp(temperatureKelvin, 2500.0, 12000.0) / 100.0;
        double red;
        double green;
        double blue;

        if (kelvin <= 66.0)
        {
            red = 255.0;
            green = 99.4708025861 * Math.Log(kelvin) - 161.1195681661;
            blue = kelvin <= 19.0 ? 0.0 : 138.5177312231 * Math.Log(kelvin - 10.0) - 305.0447927307;
        }
        else
        {
            red = 329.698727446 * Math.Pow(kelvin - 60.0, -0.1332047592);
            green = 288.1221695283 * Math.Pow(kelvin - 60.0, -0.0755148492);
            blue = 255.0;
        }

        return (
            (float)Math.Clamp(red / 255.0, 0.55, 1.0),
            (float)Math.Clamp(green / 255.0, 0.55, 1.0),
            (float)Math.Clamp(blue / 255.0, 0.55, 1.0));
    }

    /// <summary>
    /// Writes a colour into an existing <see cref="Vec4f"/> and hands it back, so the draw loop can
    /// keep one instance instead of allocating a class per body per frame.
    /// </summary>
    private static Vec4f Set(Vec4f target, float red, float green, float blue, float alpha)
    {
        target.R = red;
        target.G = green;
        target.B = blue;
        target.A = alpha;
        return target;
    }

    // Every GL call the sky pass issues goes through these three, so the diagnostic line counts the
    // work the GPU was actually asked to do rather than the length of a list that happened to match.
    private static void DrawMesh(ICoreClientAPI clientApi, MeshRef mesh)
    {
        Metrics.CountDrawCall();
        clientApi.Render.RenderMesh(mesh);
    }

    private static MeshRef UploadMesh(ICoreClientAPI clientApi, MeshData meshData)
    {
        Metrics.CountMeshUpload();
        return clientApi.Render.UploadMesh(meshData);
    }

    private static void UpdateMesh(ICoreClientAPI clientApi, MeshRef mesh, MeshData meshData)
    {
        Metrics.CountMeshUpdate();
        clientApi.Render.UpdateMesh(mesh, meshData);
    }

    private static void CopyToFloatArray(Matrix4 matrix, float[] target)
    {
        target[0] = matrix.M11;
        target[1] = matrix.M12;
        target[2] = matrix.M13;
        target[3] = matrix.M14;
        target[4] = matrix.M21;
        target[5] = matrix.M22;
        target[6] = matrix.M23;
        target[7] = matrix.M24;
        target[8] = matrix.M31;
        target[9] = matrix.M32;
        target[10] = matrix.M33;
        target[11] = matrix.M34;
        target[12] = matrix.M41;
        target[13] = matrix.M42;
        target[14] = matrix.M43;
        target[15] = matrix.M44;
    }

    private static double ToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }

    private static void LogSkyStep(float deltaTime, string message, params object[] args)
    {
        if (api is null)
        {
            return;
        }

        secondsSinceLastSkipLog += deltaTime;
        if (secondsSinceLastSkipLog < 5)
        {
            return;
        }

        secondsSinceLastSkipLog = 0;

        // Debug log, not the client's main log: this fires for as long as the sky pass is skipped,
        // which is every frame of every day, and players were reporting client-main.log full of it.
        api.Logger.VerboseDebug("AstraTerra sky step: " + message, args);
    }
}
