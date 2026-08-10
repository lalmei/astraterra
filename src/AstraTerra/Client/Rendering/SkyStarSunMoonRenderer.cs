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
    private const string StarTexturePath = "astraterra:environment/star-rays-12-smooth";
    private const string FaintStarTexturePath = "astraterra:environment/star-dog-crisp";
    private const string TelescopeStarTexturePath = "astraterra:environment/star-rays-12-smooth";
    private const string TelescopeFaintStarTexturePath = "astraterra:environment/star-dog-crisp";
    private const string ConstellationDotTexturePath = "astraterra:environment/star-pixel";
    private static readonly FieldInfo? QuadModelRefField = FindField("quadModelRef", "quadModel");
    private static readonly FieldInfo? ImageSizeField = typeof(SystemRenderSunMoon).GetField("ImageSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    private const double MinimumSkyRenderDarkness = 0.10;
    private const float MinimumStarAlpha = 0.035f;
    private const float SkyDistance = 40.0f;
    private const float MeteorSkyDistance = 39.8f;
    private const float StarAngularSizePerPixelDeg = 0.06f;
    private const float StarGlowMaxAlpha = 0.35f;
    private const float DeepSkyBrightnessScale = 0.42f;
    private const double ConstellationDotSpacingDeg = 0.65;
    private const float ConstellationDotAngularSizeDeg = 0.14f;
    private const double FaintStarMagnitudeThreshold = 2.5;

    private static ICoreClientAPI? api;
    private static AstraTerraConfig? config;
    private static StarCatalog? catalog;
    private static IReadOnlyList<MeteorShowerEntry> meteorShowers = Array.Empty<MeteorShowerEntry>();
    private static MeteorShowerVisualModel meteorVisuals = new();
    private static double secondsSinceLastLog;
    private static double secondsSinceLastSkipLog;
    private static bool starTextureLoadFailed;
    private static bool faintStarTextureLoadFailed;
    private static bool telescopeStarTextureLoadFailed;
    private static bool telescopeFaintStarTextureLoadFailed;
    private static bool constellationDotTextureLoadFailed;
    private static bool forceDaylightStars;
    private static bool renderingDisabledAfterFailure;
    private static int starTextureId;
    private static int faintStarTextureId;
    private static int telescopeStarTextureId;
    private static int telescopeFaintStarTextureId;
    private static int constellationDotTextureId;
    private static MeshRef? deepSkyQuadMesh;
    private static MeshRef? meteorStreakMesh;
    private static readonly Dictionary<string, int> DeepSkyTextureIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedDeepSkyTexturePaths = new(StringComparer.OrdinalIgnoreCase);

    public static bool ForceDaylightStars => forceDaylightStars;
    public static bool ShouldRenderVanillaStarfield
        => config is null || StarfieldModeParser.ShowsVanilla(config.GetStarfieldMode());

    public static void Initialize(
        ICoreClientAPI clientApi,
        AstraTerraConfig loadedConfig,
        StarCatalog loadedCatalog,
        IReadOnlyList<MeteorShowerEntry> loadedMeteorShowers)
    {
        ArgumentNullException.ThrowIfNull(loadedMeteorShowers);
        Reset();
        api = clientApi;
        config = loadedConfig;
        catalog = loadedCatalog;
        meteorShowers = loadedMeteorShowers;
        meteorVisuals = new MeteorShowerVisualModel((ulong)Environment.TickCount64);
        clientApi.Logger.Notification(
            "AstraTerra sky renderer initialized: renderPass=sunMoon3D; minSize={0:0.0}px; maxSize={1:0.0}px; angularScale={2:0.000}deg/px; meteorShowers={3}",
            StarBillboardSizing.MinimumCoreDiameterPixels,
            StarBillboardSizing.MaximumCoreDiameterPixels,
            StarAngularSizePerPixelDeg,
            meteorShowers.Count);
    }

    public static void Reset()
    {
        deepSkyQuadMesh?.Dispose();
        deepSkyQuadMesh = null;
        meteorStreakMesh?.Dispose();
        meteorStreakMesh = null;
        api = null;
        config = null;
        catalog = null;
        meteorShowers = Array.Empty<MeteorShowerEntry>();
        meteorVisuals.Clear();
        secondsSinceLastLog = 0;
        secondsSinceLastSkipLog = 0;
        starTextureLoadFailed = false;
        faintStarTextureLoadFailed = false;
        telescopeStarTextureLoadFailed = false;
        telescopeFaintStarTextureLoadFailed = false;
        constellationDotTextureLoadFailed = false;
        forceDaylightStars = false;
        renderingDisabledAfterFailure = false;
        starTextureId = 0;
        faintStarTextureId = 0;
        telescopeStarTextureId = 0;
        telescopeFaintStarTextureId = 0;
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
            PostfixCore(__instance, dt);
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

    private static void PostfixCore(SystemRenderSunMoon __instance, float dt)
    {
        if (api is null || config is null || catalog is null)
        {
            return;
        }

        if (!StarfieldModeParser.ShowsAstraTerra(config.GetStarfieldMode()))
        {
            meteorVisuals.Clear();
            return;
        }

        var calendar = api.World.Calendar;
        var naturalDarkness = 1.0 - calendar.DayLightStrength;
        var renderDarkness = forceDaylightStars ? 1.0 : naturalDarkness;
        if (!ShouldRenderForDarkness(naturalDarkness, forceDaylightStars))
        {
            meteorVisuals.Clear();
            LogSkyStep(dt, "skipped daylight: daylight={0:0.00}; darkness={1:0.00}", calendar.DayLightStrength, naturalDarkness);
            return;
        }

        var playerPos = api.World.Player.Entity.Pos;
        if (!HasOpenSkyAbovePlayer(api))
        {
            meteorVisuals.Clear();
            LogSkyStep(dt, "skipped blocked sky: x={0:0.0}; y={1:0.0}; z={2:0.0}", playerPos.X, playerPos.Y, playerPos.Z);
            return;
        }

        var latitude = LatitudeMapper.MapGameLatitude(playerPos.Z, calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(playerPos.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);
        var brightnessBias = config.StarBrightnessBias * (float)renderDarkness;
        var visibleStars = StarRenderModel.ProjectVisibleStars(catalog.Stars, latitude, localSiderealAngle, brightnessBias);
        var drawableStars = FilterDrawableStars(visibleStars);
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
        var meteorStreaks = meteorVisuals.Advance(dt, meteorReadings);
        if (drawableStars.Count == 0 && meteorStreaks.Count == 0)
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
            return;
        }

        var journal = ConstellationBookService.ReadJournal(api.World.Player.Entity.LeftHandItemSlot?.Itemstack);
        IReadOnlyList<SkyConstellationDot> constellationDots = Array.Empty<SkyConstellationDot>();
        if (journal is not null)
        {
            var visibleStarMap = drawableStars.ToDictionary(star => star.Hip);
            var constellationSegments = ConstellationRenderModel.BuildConstellationSegments(journal.Constellations, visibleStarMap);
            constellationDots = ConstellationRenderModel.BuildSkyDots(constellationSegments, ConstellationDotSpacingDeg);
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
            return;
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
            return;
        }

        RenderSky(
            api,
            drawableStars,
            visibleDeepSkyObjects,
            constellationDots,
            meteorStreaks,
            meteorReadings.FirstOrDefault(reading => reading.ObservedHourlyRate > 0.0),
            quadModel,
            imageSize,
            dt,
            calendar.DayLightStrength,
            playerPos.Yaw,
            playerPos.Pitch,
            forceDaylightStars);
    }

    private static void RenderSky(
        ICoreClientAPI clientApi,
        IReadOnlyList<RenderedStar> visibleStars,
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

            foreach (var star in visibleStars)
            {
                var isFaint = star.VisualMagnitude > FaintStarMagnitudeThreshold;
                var textureId = isFaint ? dimStarTextureId : brightStarTextureId;
                var size = StarBillboardSizing.CalculateCoreDiameterPixels(star.Size);
                var alpha = (float)Math.Clamp(star.Brightness, 0.0, 1.0);
                var tint = ColorFromTemperature(star.ColorTemperatureK, alpha);
                var outerAlpha = StarGlowMaxAlpha * alpha * alpha;
                if (!isFaint && outerAlpha > 0.005f)
                {
                    var glowSize = StarBillboardSizing.CalculateGlowDiameterPixels(size, alpha);
                    var glowTint = new Vec4f(tint.R, tint.G, tint.B, outerAlpha);
                    RenderStarQuad(clientApi, shader, quadModel, star, glowSize, imageSize, brightStarTextureId, glowTint, modelMatrixBuffer);
                }

                RenderStarQuad(clientApi, shader, quadModel, star, size, imageSize, textureId, tint, modelMatrixBuffer);

                drawnCount++;
                smallestDrawnSize = Math.Min(smallestDrawnSize, size);
                largestDrawnSize = Math.Max(largestDrawnSize, size);
            }

            if (visibleDeepSkyObjects.Count > 0)
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

            if (constellationDotTextureId != 0)
            {
                foreach (var dot in constellationDots)
                {
                    RenderConstellationDot(clientApi, shader, quadModel, dot, imageSize, modelMatrixBuffer);
                }
            }

            if (meteorStreakMesh is not null && meteorStreaks.Count > 0 && constellationDotTextureId != 0)
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

        secondsSinceLastLog += deltaTime;
        if (secondsSinceLastLog >= 5)
        {
            secondsSinceLastLog = 0;
            clientApi.Logger.Notification(
                "AstraTerra sky draw: pass=sunMoon3D; visible={0}; drawn={1}; constellationDots={2}; daylight={3:0.00}; forceDaylight={4}; azimuth={5:0.0}; altitude={6:0.0}",
                visibleStars.Count,
                drawnCount,
                constellationDots.Count,
                daylight,
                forceDaylight,
                CelestialMath.NormalizeDegrees(ToDegrees(yaw)),
                Math.Clamp(-ToDegrees(pitch), -89.0, 89.0));
            if (visibleDeepSkyObjects.Count > 0)
            {
                clientApi.Logger.Notification(
                    "AstraTerra telescope deep-sky draw: visible={0}",
                    visibleDeepSkyObjects.Count);
            }
            clientApi.Logger.Notification(
                "AstraTerra sky size: minConfigured={0:0.0}px; scale={1:0.0}; maxConfigured={2:0.0}px; drawnSizeRange={3:0.0}-{4:0.0}px; telescopeSprites={5}",
                StarBillboardSizing.MinimumCoreDiameterPixels,
                StarBillboardSizing.ProjectedSizeScale,
                StarBillboardSizing.MaximumCoreDiameterPixels,
                drawnCount == 0 ? 0 : smallestDrawnSize,
                largestDrawnSize,
                useTelescopeSprites);
            if (strongestMeteorReading is not null || meteorStreaks.Count > 0)
            {
                clientApi.Logger.Notification(
                    "AstraTerra meteor shower: strongest={0}; rate={1:0.0}/h; radiantAz={2:0.0}; radiantAlt={3:0.0}; activeStreaks={4}",
                    strongestMeteorReading?.Shower.DisplayName ?? "none",
                    strongestMeteorReading?.ObservedHourlyRate ?? 0.0,
                    strongestMeteorReading?.AzimuthDeg ?? 0.0,
                    strongestMeteorReading?.AltitudeDeg ?? 0.0,
                    meteorStreaks.Count);
            }
        }
    }

    private static void RenderStarQuad(
        ICoreClientAPI clientApi,
        IStandardShaderProgram shader,
        MeshRef quadModel,
        RenderedStar star,
        float sizePixels,
        int imageSize,
        int textureId,
        Vec4f tint,
        float[] modelMatrixBuffer)
    {
        var modelMatrix = BuildModelMatrix(clientApi, star, sizePixels, imageSize);

        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = tint;
        shader.RgbaLightIn = new Vec4f(tint.R, tint.G, tint.B, 1f);
        shader.Tex2D = textureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        clientApi.Render.RenderMesh(quadModel);
        ((IShaderProgram)shader).Uniform("skyShaded", 0);
    }

    private static void RenderConstellationDot(
        ICoreClientAPI clientApi,
        IStandardShaderProgram shader,
        MeshRef quadModel,
        SkyConstellationDot dot,
        int imageSize,
        float[] modelMatrixBuffer)
    {
        var modelMatrix = BuildSkyBillboardMatrix(
            clientApi,
            dot.DirectionX,
            dot.DirectionY,
            dot.DirectionZ,
            ConstellationDotAngularSizeDeg,
            imageSize);

        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = new Vec4f(dot.Tint.R, dot.Tint.G, dot.Tint.B, dot.Tint.A);
        shader.RgbaLightIn = new Vec4f(dot.Tint.R, dot.Tint.G, dot.Tint.B, 1f);
        shader.Tex2D = constellationDotTextureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        clientApi.Render.RenderMesh(quadModel);
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
            meteorStreakMesh = clientApi.Render.UploadMesh(meshData);
        }
        else
        {
            clientApi.Render.UpdateMesh(meteorStreakMesh, meshData);
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
        clientApi.Render.RenderMesh(meteorStreakMesh);
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
            deepSkyQuadMesh = clientApi.Render.UploadMesh(meshData);
        }
        else
        {
            clientApi.Render.UpdateMesh(deepSkyQuadMesh, meshData);
        }

        var entity = clientApi.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - clientApi.World.SeaLevel) / 10000f;
        var modelMatrix = Matrix4.CreateTranslation(0f, verticalOrigin, 0f);
        var alpha = (float)Math.Clamp(deepSkyObject.Brightness * DeepSkyBrightnessScale, 0.0, 0.7);
        var tint = new Vec4f(
            Math.Clamp(deepSkyObject.TintR, 0.0f, 1.0f),
            Math.Clamp(deepSkyObject.TintG, 0.0f, 1.0f),
            Math.Clamp(deepSkyObject.TintB, 0.0f, 1.0f),
            alpha);

        ((IShaderProgram)shader).Uniform("skyShaded", 0);
        shader.RgbaTint = tint;
        shader.RgbaLightIn = new Vec4f(tint.R, tint.G, tint.B, 1f);
        shader.Tex2D = textureId;
        CopyToFloatArray(modelMatrix, modelMatrixBuffer);
        ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrixBuffer);
        clientApi.Render.RenderMesh(deepSkyQuadMesh);
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

    private static Matrix4 BuildModelMatrix(ICoreClientAPI clientApi, RenderedStar star, float sizePixels, int imageSize)
    {
        var angularSizeDeg = Math.Clamp(sizePixels * StarAngularSizePerPixelDeg, 0.01f, 2.0f);
        return BuildSkyBillboardMatrix(clientApi, star.DirectionX, star.DirectionY, star.DirectionZ, angularSizeDeg, imageSize);
    }

    private static Matrix4 BuildSkyBillboardMatrix(
        ICoreClientAPI clientApi,
        double directionX,
        double directionY,
        double directionZ,
        float angularSizeDeg,
        int imageSize)
    {
        var entity = clientApi.World.Player.Entity;
        var y = (float)(directionY * SkyDistance)
            + (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - clientApi.World.SeaLevel) / 10000f;
        var position = new Vector3(
            (float)(directionX * SkyDistance),
            y,
            (float)(directionZ * SkyDistance));
        var rotation = SystemRenderSunMoon.CreateLookRotation(position);
        var worldSize = 2f * SkyDistance * MathF.Tan(angularSizeDeg * MathF.PI / 360f);
        var scale = Math.Max(0.0005f, worldSize / Math.Max(1, imageSize));

        return Matrix4.CreateTranslation(-imageSize / 2f, -imageSize / 2f, 0f)
            * Matrix4.CreateScale(scale)
            * Matrix4.CreateFromQuaternion(rotation)
            * Matrix4.CreateTranslation(position);
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

    private static bool HasOpenSkyAbovePlayer(ICoreClientAPI clientApi)
    {
        var entity = clientApi.World.Player.Entity;
        var blockPos = entity.Pos.AsBlockPos;
        var eyeY = entity.Pos.InternalY + entity.LocalEyePos.Y;
        return clientApi.World.BlockAccessor.GetRainMapHeightAt(blockPos) <= eyeY;
    }

    private static IReadOnlyList<RenderedStar> FilterDrawableStars(IReadOnlyList<RenderedStar> visibleStars)
    {
        var drawableStars = new List<RenderedStar>(visibleStars.Count);
        foreach (var star in visibleStars)
        {
            if (star.Brightness >= MinimumStarAlpha)
            {
                drawableStars.Add(star);
            }
        }

        return drawableStars;
    }

    private static void EnsureStarTextures(ICoreClientAPI clientApi)
    {
        EnsureStarTexture(clientApi, StarTexturePath, ref starTextureId, ref starTextureLoadFailed);
        EnsureStarTexture(clientApi, FaintStarTexturePath, ref faintStarTextureId, ref faintStarTextureLoadFailed);
        EnsureStarTexture(clientApi, TelescopeStarTexturePath, ref telescopeStarTextureId, ref telescopeStarTextureLoadFailed);
        EnsureStarTexture(clientApi, TelescopeFaintStarTexturePath, ref telescopeFaintStarTextureId, ref telescopeFaintStarTextureLoadFailed);
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

    private static Vec4f ColorFromTemperature(double temperatureKelvin, float alpha)
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

        return new Vec4f(
            (float)Math.Clamp(red / 255.0, 0.55, 1.0),
            (float)Math.Clamp(green / 255.0, 0.55, 1.0),
            (float)Math.Clamp(blue / 255.0, 0.55, 1.0),
            alpha);
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
        api.Logger.Notification("AstraTerra sky step: " + message, args);
    }
}
