using AstraTerra.Astronomy;
using AstraTerra.Client.Observation;
using AstraTerra.Client.Rendering;
using AstraTerra.Client.SkyLying;
using AstraTerra.Client.Zoom;
using AstraTerra.Commands;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Items;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra;

public sealed class AstraTerraModSystem : ModSystem
{
    private AstraTerraConfig? config;
    private StarCatalog? catalog;
    private IReadOnlyList<MeteorShowerEntry> meteorShowers = Array.Empty<MeteorShowerEntry>();
    private PlanetCatalog? planets;
    private CometCatalog? comets;
    private TelescopeZoomPatcher? telescopeZoomPatcher;
    private TelescopeScopeController? telescopeScopeController;
    private SkyLyingController? skyLyingController;
    private GroundStorageDiscMeshPatcher? groundStorageDiscMeshPatcher;
    private ICoreClientAPI? clientApi;
    private ConstellationOverlayRenderer? constellationOverlayRenderer;
    private ConstellationBookClient? constellationBookClient;
    private AstrolabePlannerRenderer? astrolabePlannerRenderer;
    private SextantReadingRenderer? sextantReadingRenderer;
    private SkyCoordinateGridRenderer? skyCoordinateGridRenderer;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(AstraTerraModMetadata.StartupLogMessage);
        api.RegisterItemClass("AstraTerra.Items.ItemBrassTelescope", typeof(ItemBrassTelescope));
        api.RegisterItemClass("AstraTerra.Items.ItemPrecisionTelescope", typeof(ItemPrecisionTelescope));
        api.RegisterItemClass("AstraTerra.Items.ItemSextant", typeof(ItemSextant));
        api.RegisterItemClass("AstraTerra.Items.ItemAstrolabe", typeof(ItemAstrolabe));
        api.RegisterItemClass("AstraTerra.Items.ItemSkyDisc", typeof(ItemSkyDisc));
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemBrassTelescope");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemPrecisionTelescope");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemSextant");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemAstrolabe");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemSkyDisc");
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is ICoreClientAPI clientApi)
        {
            config = AstraTerraConfigLoader.Load(clientApi);
            api.Logger.Event(
                "AstraTerra startup step: config loaded: starfieldMode={0}; skyGridMode={1}; starBrightnessBias={2:0.00}; showMinimalHud={3}; showReticle={4}; debugGuideStarEmphasis={5}; debugMeteorRateMultiplier={6:0.00}",
                config.StarfieldMode,
                config.SkyGridMode,
                config.StarBrightnessBias,
                config.ShowMinimalHud,
                config.ShowReticle,
                config.DebugGuideStarEmphasisDefault,
                config.DebugMeteorRateMultiplier);
        }

        try
        {
            catalog = StarCatalogLoader.Load(
                api,
                "astraterra:data/star-catalog.v1.json",
                "astraterra:data/guide-stars.v1.json",
                "astraterra:data/sky-cultures.v1.json",
                "astraterra:data/deep-sky.v1.json");
            api.Logger.Event(
                "AstraTerra startup step: astronomy catalog loaded: stars={0}; guideGroups={1}; skyCultures={2}; deepSkyObjects={3}",
                catalog.Stars.Count,
                catalog.GuideGroups.Count,
                catalog.SkyCultures.Count,
                catalog.DeepSkyObjects.Count);
        }
        catch (Exception exception)
        {
            api.Logger.Error("AstraTerra astronomy disabled: {0}", exception);
            if (api is ICoreClientAPI errorClientApi)
            {
                errorClientApi.TriggerIngameError(this, "astraterra:initfailed", "AstraTerra disabled astronomy due to asset or init failure.");
            }
        }

        try
        {
            meteorShowers = MeteorShowerCatalogLoader.Load(api);
            api.Logger.Event(
                "AstraTerra startup step: meteor shower catalog loaded: showers={0}",
                meteorShowers.Count);
        }
        catch (Exception exception)
        {
            meteorShowers = Array.Empty<MeteorShowerEntry>();
            api.Logger.Warning("AstraTerra meteor showers disabled: {0}", exception);
        }

        try
        {
            planets = PlanetCatalogLoader.Load(api);
            api.Logger.Event(
                "AstraTerra startup step: planet catalog loaded: planets={0}",
                planets.Planets.Count);
        }
        catch (Exception exception)
        {
            planets = null;
            api.Logger.Warning("AstraTerra planets disabled: {0}", exception);
        }

        try
        {
            comets = CometCatalogLoader.Load(api);
            api.Logger.Event(
                "AstraTerra startup step: comet catalog loaded: comets={0}",
                comets.Comets.Count);
        }
        catch (Exception exception)
        {
            comets = null;
            api.Logger.Warning("AstraTerra comets disabled: {0}", exception);
        }
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);

        // Belt and braces with assets/astraterra/patches/book-normal-offhand.json: if that patch is
        // ever lost -- another mod rewriting the book item type, say -- the off-hand slot silently
        // stops accepting journals and every held-book feature goes quiet.
        var offhandBooks = BookOffhandStorage.Apply(api);
        api.Logger.Event("AstraTerra startup step: journal books allowed in the off-hand: items={0}", offhandBooks);
        ConstellationPreparedBooks.RegisterCreativeStacks(api, catalog, planets);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        SkyStarSunMoonRenderer.Reset();
        AstrolabeReadingState.Reset();
        AstrolabeCalibrationState.Reset();
        SextantReadingState.Reset();
        SkyDiscReadingState.Reset();
        SkyDiscMeshes.Reset();
        SkyLyingState.Reset();
        clientApi = api;
        config ??= AstraTerraConfigLoader.Load(api);
        skyLyingController = new SkyLyingController();
        skyLyingController.Start(api);
        api.Logger.Event("AstraTerra startup step: lie-down pose registered: hotkey={0}", SkyLyingController.HotkeyCode);
        telescopeScopeController = new TelescopeScopeController();
        telescopeScopeController.Start(api);
        telescopeZoomPatcher = new TelescopeZoomPatcher();
        telescopeZoomPatcher.Start(api);
        api.Logger.Event("AstraTerra startup step: telescope zoom patched");
        api.Event.MouseWheelMove += OnMouseWheelMove;
        api.Logger.Event("AstraTerra startup step: observation input registered: mouseWheelZoom=true");
        var store = new ConstellationJournalStore(api.GetOrCreateDataPath("AstraTerra"));
        var worldIdentifier = api.World.SavegameIdentifier;
        var legacyJournal = store.LoadUnmigrated(worldIdentifier);
        api.Logger.Event(
            "AstraTerra startup step: legacy constellation journal loaded: world={0}; records={1}; migrated={2}",
            worldIdentifier,
            legacyJournal.Constellations.Count,
            store.IsMigrated(worldIdentifier));
        constellationBookClient = new ConstellationBookClient(api, legacyJournal, () => store.MarkMigrated(worldIdentifier));
        constellationBookClient.Register();
        new StarsClientCommands(
            journal => new StarsCommandService(
                journal,
                catalog: catalog,
                latitudeProvider: () => LatitudeMapper.MapGameLatitude(
                    api.World.Player.Entity.Pos.Z,
                    api.World.Calendar.OnGetLatitude is null ? null : z => api.World.Calendar.OnGetLatitude(z)),
                latitudeWrapCycleProvider: () => LatitudeMapper.GetWorldLatitudeWrapCycle(api.World.Player.Entity.Pos.Z, api.World.DefaultSpawnPosition.Z),
                siderealAngleProvider: () =>
                {
                    var calendar = api.World.Calendar;
                    var position = api.World.Player.Entity.Pos;
                    var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
                    return CelestialMath.GetVanillaAlignedLocalSiderealAngle(
                        calendar.TotalDays,
                        Math.Max(1, calendar.DaysPerYear),
                        Math.Max(1.0, calendar.HoursPerDay),
                        longitude);
                },
                dayOfYearProvider: () => api.World.Calendar.DayOfYear,
                daysPerYearProvider: () => Math.Max(1, api.World.Calendar.DaysPerYear)),
            constellationBookClient,
            config,
            comets,
            planets).Register(api);
        api.Logger.Event("AstraTerra startup step: client commands registered: .stars list/info/build/connect/name/select/delete/comets/debug/daylight-stars/starfield/sky-grid/render");

        skyCoordinateGridRenderer = new SkyCoordinateGridRenderer(api, config);
        api.Event.RegisterRenderer(skyCoordinateGridRenderer, EnumRenderStage.Opaque, "AstraTerraSkyCoordinateGrid");

        // Sun and moon sighting reads Vintage Story directly and needs nothing from the star
        // catalog, so the sextant is registered before the catalog gate and keeps working without it.
        // The disc needs nothing from any catalog: it watches the sun Vintage Story already draws.
        SkyDiscMeshes.Install(api);
        groundStorageDiscMeshPatcher = new GroundStorageDiscMeshPatcher();
        api.Logger.Event(
            "AstraTerra startup step: sky disc marks on stored discs: {0}",
            groundStorageDiscMeshPatcher.Start(api) ? "patched" : "unavailable");
        api.Event.RegisterRenderer(new SkyDiscRenderer(api), EnumRenderStage.Ortho, "AstraTerraSkyDisc");

        sextantReadingRenderer = new SextantReadingRenderer(api, config, catalog, planets, comets, constellationBookClient);
        api.Event.RegisterRenderer(sextantReadingRenderer, EnumRenderStage.Opaque, "AstraTerraSextantMatrixCapture");
        api.Event.RegisterRenderer(sextantReadingRenderer, EnumRenderStage.Ortho, "AstraTerraSextantReading");
        api.Event.MouseDown += sextantReadingRenderer.OnMouseDown;

        if (catalog is null)
        {
            api.Logger.Event("AstraTerra startup step: client renderers skipped: catalog-dependent astronomy renderers were not registered; sky coordinate grid and sun/moon sextant sighting remain available");
            return;
        }

        SkyStarSunMoonRenderer.Initialize(api, config, catalog, meteorShowers, planets, comets);
        constellationOverlayRenderer = new ConstellationOverlayRenderer(api, config, catalog, constellationBookClient, planets);
        api.Event.RegisterRenderer(constellationOverlayRenderer, EnumRenderStage.Opaque, "AstraTerraOverlayMatrixCapture");
        api.Event.RegisterRenderer(constellationOverlayRenderer, EnumRenderStage.Ortho, "AstraTerraOverlay");
        api.Event.MouseDown += constellationOverlayRenderer.OnMouseDown;
        api.Event.MouseMove += constellationOverlayRenderer.OnMouseMove;
        api.Event.MouseUp += constellationOverlayRenderer.OnMouseUp;
        api.Event.RegisterRenderer(new TelescopeScopeRenderer(api), EnumRenderStage.Ortho, "AstraTerraTelescopeScope");
        astrolabePlannerRenderer = new AstrolabePlannerRenderer(api, catalog, constellationBookClient, planets, comets);
        api.Event.RegisterRenderer(astrolabePlannerRenderer, EnumRenderStage.Ortho, "AstraTerraAstrolabePlanner");
        api.Event.MouseDown += astrolabePlannerRenderer.OnMouseDown;
        api.Logger.Event(
            "AstraTerra startup step: client renderers registered: skyPatch=SystemRenderSunMoon.OnRenderFrame3D; skyGrid=AstraTerraSkyCoordinateGrid; overlay=AstraTerraOverlayMatrixCapture+AstraTerraOverlay; telescopeScope=AstraTerraTelescopeScope; sextant=AstraTerraSextantMatrixCapture+AstraTerraSextantReading; astrolabe=AstraTerraAstrolabePlanner; stars={0}; guideGroups={1}; skyCultures={2}; deepSkyObjects={3}; meteorShowers={4}; planets={5}; comets={6}",
            catalog.Stars.Count,
            catalog.GuideGroups.Count,
            catalog.SkyCultures.Count,
            catalog.DeepSkyObjects.Count,
            meteorShowers.Count,
            planets?.Planets.Count ?? 0,
            comets?.Comets.Count ?? 0);
    }

    /// <summary>
    /// Replaces the star catalog for the rest of the session, for mods that author a starfield from
    /// the world seed instead of shipping it as an asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The asset catalog is loaded in <c>AssetsLoaded</c>, well before a client knows which world it
    /// is joining, so a procedural sky cannot be supplied that early. Such a mod lets the shipped
    /// catalog load, then calls this once the server has told it what this save's sky is.
    /// </para>
    /// <para>
    /// Constellations are stored as edges between <c>Hip</c> ids, so a caller that swaps the catalog
    /// owns the stability of those ids: the same world has to keep numbering the same stars, or
    /// every figure a player has recorded will point somewhere else.
    /// </para>
    /// </remarks>
    /// <returns>False when astronomy is disabled because no catalog loaded at startup.</returns>
    public bool ReplaceStarCatalog(StarCatalog replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (catalog is null)
        {
            return false;
        }

        catalog = replacement;
        SkyStarSunMoonRenderer.ReplaceCatalog(replacement);
        constellationOverlayRenderer?.ReplaceCatalog(replacement);
        astrolabePlannerRenderer?.ReplaceCatalog(replacement);
        sextantReadingRenderer?.ReplaceCatalog(replacement);
        return true;
    }

    /// <summary>
    /// Replaces the planets, or passes null for a sky that has none.
    /// </summary>
    /// <remarks>
    /// The shipped planets are this solar system's, with orbits authored against it, so a mod that
    /// moves the world elsewhere has to either supply its own or say there are none. Passing null
    /// takes the same path as a planet catalog that failed to load, which every consumer already
    /// handles by leaving planets out.
    /// </remarks>
    public void ReplacePlanetCatalog(PlanetCatalog? replacement)
    {
        planets = replacement;
        SkyStarSunMoonRenderer.ReplacePlanetCatalog(replacement);
        constellationOverlayRenderer?.ReplacePlanetCatalog(replacement);
        astrolabePlannerRenderer?.ReplacePlanetCatalog(replacement);
        sextantReadingRenderer?.ReplacePlanetCatalog(replacement);
    }

    /// <summary>Replaces the comets, or passes null for a sky that has none.</summary>
    public void ReplaceCometCatalog(CometCatalog? replacement)
    {
        comets = replacement;
        SkyStarSunMoonRenderer.ReplaceCometCatalog(replacement);
        astrolabePlannerRenderer?.ReplaceCometCatalog(replacement);
        sextantReadingRenderer?.ReplaceCometCatalog(replacement);
    }

    /// <summary>
    /// Replaces the meteor showers; an empty list is a sky with none.
    /// </summary>
    /// <remarks>
    /// The shipped showers are named for the constellations their radiants sit in, so they depend on
    /// a sky culture as well as on the debris streams of the shipped comets. Under a replaced sky
    /// both of those are gone, and a radiant named for a figure nobody can point to reads as a bug.
    /// </remarks>
    public void ReplaceMeteorShowers(IReadOnlyList<MeteorShowerEntry> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        meteorShowers = replacement;
        SkyStarSunMoonRenderer.ReplaceMeteorShowers(replacement);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        new ConstellationBookServer(() => catalog).Register(api);
        new StarsServerCommands(() => catalog, () => planets).Register(api);
        api.Logger.Event("AstraTerra startup step: server commands registered: /stars debug/goto-lat/give-catalog/give-zodiac");
    }

    public override void Dispose()
    {
        telescopeZoomPatcher?.Stop();
        telescopeScopeController?.Stop();
        skyLyingController?.Stop();
        SkyStarSunMoonRenderer.Reset();
        AstrolabeReadingState.Reset();
        AstrolabeCalibrationState.Reset();
        SextantReadingState.Reset();
        SkyDiscReadingState.Reset();
        groundStorageDiscMeshPatcher?.Stop();
        SkyLyingState.Reset();
        skyCoordinateGridRenderer?.Dispose();
        if (clientApi is not null && constellationOverlayRenderer is not null)
        {
            clientApi.Event.MouseDown -= constellationOverlayRenderer.OnMouseDown;
            clientApi.Event.MouseMove -= constellationOverlayRenderer.OnMouseMove;
            clientApi.Event.MouseUp -= constellationOverlayRenderer.OnMouseUp;
        }

        if (clientApi is not null)
        {
            clientApi.Event.MouseWheelMove -= OnMouseWheelMove;
        }

        if (clientApi is not null && astrolabePlannerRenderer is not null)
        {
            clientApi.Event.MouseDown -= astrolabePlannerRenderer.OnMouseDown;
        }

        if (clientApi is not null && sextantReadingRenderer is not null)
        {
            clientApi.Event.MouseDown -= sextantReadingRenderer.OnMouseDown;
        }
    }

    private void OnMouseWheelMove(MouseWheelEventArgs args)
    {
        if (TelescopeScopeState.IsScoped)
        {
            TelescopeScopeState.ScrollZoom(args.delta > 0 ? 1 : -1);
            args.SetHandled(true);
            return;
        }

        if (!AstrolabeReadingState.IsReading || clientApi is null)
        {
            return;
        }

        var calendar = clientApi.World.Calendar;
        var hoursPerDay = Math.Max(1.0, calendar.HoursPerDay);
        var stepHours = clientApi.World.Player.Entity.Controls.Sneak
            ? hoursPerDay * 7.0
            : 1.0;
        AstrolabeReadingState.AdjustForecastHours(
            args.delta > 0 ? stepHours : -stepHours,
            Math.Max(1, calendar.DaysPerYear) * hoursPerDay);
        args.SetHandled(true);
    }
}
