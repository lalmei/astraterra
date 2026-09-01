using AstraTerra.Astronomy;
using AstraTerra.Client.Calendar;
using AstraTerra.Client.Observation;
using AstraTerra.Client.Rendering;
using AstraTerra.Client.SkyLying;
using AstraTerra.Client.Zoom;
using AstraTerra.Commands;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Items;
using AstraTerra.Items.Patches;
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
    private ICoreClientAPI? clientApi;
    private ConstellationOverlayRenderer? constellationOverlayRenderer;
    private ConstellationBookClient? constellationBookClient;
    private SkyDiscEngraveClient? skyDiscEngraveClient;
    private readonly PitKilnFiringKeepsTheDiscPatch skyDiscFiringPatch = new();
    private SkyDiscFaceHud? skyDiscFaceHud;
    private AstrolabePlannerRenderer? astrolabePlannerRenderer;
    private SextantReadingRenderer? sextantReadingRenderer;
    private SkyCoordinateGridRenderer? skyCoordinateGridRenderer;
    private NearBodyRenderer? nearBodyRenderer;
    private MoonDiscRenderer? moonDiscRenderer;
    private NearBodyCatalog nearBodies = NearBodyCatalog.Empty;
    private LongitudeAwareSunInstaller? clientLongitudeAwareSunInstaller;
    private LongitudeAwareSunInstaller? serverLongitudeAwareSunInstaller;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(AstraTerraModMetadata.StartupLogMessage);
        api.RegisterItemClass("AstraTerra.Items.ItemBrassTelescope", typeof(ItemBrassTelescope));
        api.RegisterItemClass("AstraTerra.Items.ItemPrecisionTelescope", typeof(ItemPrecisionTelescope));
        api.RegisterItemClass("AstraTerra.Items.ItemSextant", typeof(ItemSextant));
        api.RegisterItemClass("AstraTerra.Items.ItemAstrolabe", typeof(ItemAstrolabe));
        api.RegisterItemClass("AstraTerra.Items.ItemSkyDisc", typeof(ItemSkyDisc));

        // Both sides: firing runs on the server, and in single player that server is this process.
        api.Logger.Event(
            "AstraTerra startup step: clay disc firing patched: kiln={0}",
            skyDiscFiringPatch.Start(api));
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemBrassTelescope");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemPrecisionTelescope");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemSextant");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemAstrolabe");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemSkyDisc");
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        config = AstraTerraConfigLoader.Load(api);
        if (api is ICoreClientAPI clientApi)
        {
            api.Logger.Event(
                "AstraTerra startup step: config loaded: starfieldMode={0}; skyGridMode={1}; solarSystemArt={2}; moonArt={3}; starBrightnessBias={4:0.00}; showMinimalHud={5}; showReticle={6}; debugGuideStarEmphasis={7}; debugMeteorRateMultiplier={8:0.00}; longitudeAwareSun(local)={9}; displayedClockTime={10}",
                config.StarfieldMode,
                config.SkyGridMode,
                config.SolarSystemArt,
                config.MoonArt,
                config.StarBrightnessBias,
                config.ShowMinimalHud,
                config.ShowReticle,
                config.DebugGuideStarEmphasisDefault,
                config.DebugMeteorRateMultiplier,
                config.LongitudeAwareSun,
                config.DisplayedClockTime);
        }
        else
        {
            api.Logger.Event(
                "AstraTerra startup step: config loaded: longitudeAwareSun={0}",
                config.LongitudeAwareSun);
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
        clientLongitudeAwareSunInstaller = LongitudeAwareSunInstaller.StartClient(api);
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
        // Both live behind the one Harmony instance below, so the calendar hooks have to know their
        // config before anything is patched.
        VanillaCalendarHooks.Initialize(api, config);
        telescopeZoomPatcher = new TelescopeZoomPatcher();
        telescopeZoomPatcher.Start(api);
        api.Logger.Event(
            "AstraTerra startup step: telescope zoom patched; vanilla calendar display={0}",
            config.CalendarDisplay);
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
                    var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World);
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
        api.Logger.Event("AstraTerra startup step: client commands registered: .stars list/info/build/connect/name/select/delete/comets/debug/daylight-stars/starfield/sky-grid/calendar/render");

        skyCoordinateGridRenderer = new SkyCoordinateGridRenderer(api, config);
        api.Event.RegisterRenderer(skyCoordinateGridRenderer, EnumRenderStage.Opaque, "AstraTerraSkyCoordinateGrid");

        // Near bodies need no star catalog: a world whose sky is dominated by the planet it orbits
        // should show that planet whether or not the starfield loaded.
        nearBodyRenderer = new NearBodyRenderer(api);
        nearBodyRenderer.Apply(nearBodies);
        api.Event.RegisterRenderer(nearBodyRenderer, EnumRenderStage.Opaque, "AstraTerraNearBodies");

        // The moon needs no star catalog either: it draws what Vintage Story's own calendar already
        // says, from AstraTerra's photographs of the real one. Registered after the near bodies so
        // that a moon world, which has no moon, wins the argument over the vanilla disc.
        moonDiscRenderer = new MoonDiscRenderer(api, config);
        api.Event.RegisterRenderer(moonDiscRenderer, EnumRenderStage.Opaque, "AstraTerraMoonDisc");

        // Sun and moon sighting reads Vintage Story directly and needs nothing from the star
        // catalog, so the sextant is registered before the catalog gate and keeps working without it.
        // The solar band needs no catalog. A figure on the same disc does: its stored star ids are
        // projected onto the face when a catalog is available, and the rest of the instrument keeps
        // working if astronomy failed to load.
        SkyDiscMeshes.Install(api, catalog);
        api.Event.RegisterRenderer(new SkyDiscRenderer(api), EnumRenderStage.Ortho, "AstraTerraSkyDisc");

        // Held open for the session: it draws nothing until a disc is raised, and it has to be an
        // open dialog to be drawn at all.
        skyDiscFaceHud = new SkyDiscFaceHud(api);
        skyDiscFaceHud.TryOpen();

        skyDiscEngraveClient = new SkyDiscEngraveClient(api);
        skyDiscEngraveClient.Register();

        sextantReadingRenderer = new SextantReadingRenderer(api, config, catalog, planets, comets, constellationBookClient, nearBodies);
        api.Event.RegisterRenderer(sextantReadingRenderer, EnumRenderStage.Opaque, "AstraTerraSextantMatrixCapture");
        api.Event.RegisterRenderer(sextantReadingRenderer, EnumRenderStage.Ortho, "AstraTerraSextantReading");
        api.Event.MouseDown += sextantReadingRenderer.OnMouseDown;

        if (catalog is null)
        {
            api.Logger.Event("AstraTerra startup step: client renderers skipped: catalog-dependent astronomy renderers were not registered; sky coordinate grid and sun/moon sextant sighting remain available");
            return;
        }

        SkyStarSunMoonRenderer.Initialize(api, config, catalog, meteorShowers, planets, comets);
        constellationOverlayRenderer = new ConstellationOverlayRenderer(api, config, catalog, constellationBookClient, skyDiscEngraveClient);
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
        SkyDiscMeshes.Active?.ReplaceCatalog(replacement);
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

    /// <summary>
    /// Replaces the bodies near enough to show a disc -- the planet a moon world orbits, and the
    /// moons that share that orbit -- or passes an empty catalog for an ordinary sky.
    /// </summary>
    /// <remarks>
    /// Vintage Story draws one moon of its own. A world that is itself a moon has none, so the
    /// catalog can ask for that moon to stand down rather than hang beside a parent planet it has
    /// nothing to do with. Only the drawing stops: moonlight, the phase the calendar reports, and
    /// the length of the day are all untouched.
    /// </remarks>
    public void ReplaceNearBodies(NearBodyCatalog? replacement)
    {
        nearBodies = replacement ?? NearBodyCatalog.Empty;
        nearBodyRenderer?.Apply(nearBodies);
        sextantReadingRenderer?.ReplaceNearBodies(nearBodies);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        config ??= AstraTerraConfigLoader.Load(api);
        serverLongitudeAwareSunInstaller = LongitudeAwareSunInstaller.StartServer(api, config.LongitudeAwareSun);
        new ConstellationBookServer(() => catalog).Register(api);
        new SkyDiscEngraveServer().Register(api);
        new StarsServerCommands(() => catalog, () => planets).Register(api);
        api.Logger.Event("AstraTerra startup step: server commands registered: /stars debug/goto-lat/give-catalog/give-zodiac");
    }

    public override void Dispose()
    {
        telescopeZoomPatcher?.Stop();
        skyDiscFiringPatch.Stop();
        clientLongitudeAwareSunInstaller?.Dispose();
        serverLongitudeAwareSunInstaller?.Dispose();
        VanillaCalendarHooks.Reset();
        telescopeScopeController?.Stop();
        skyLyingController?.Stop();
        SkyStarSunMoonRenderer.Reset();
        AstrolabeReadingState.Reset();
        AstrolabeCalibrationState.Reset();
        SextantReadingState.Reset();
        SkyDiscReadingState.Reset();
        skyDiscFaceHud?.TryClose();
        SkyLyingState.Reset();
        skyCoordinateGridRenderer?.Dispose();
        nearBodyRenderer?.Dispose();
        moonDiscRenderer?.Dispose();
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

        // A disc is turned in the hand, not zoomed or wound forward: the wheel spins it about its
        // own face so a mark can be brought round to where its owner is looking.
        if (SkyDiscReadingState.IsReading)
        {
            SkyDiscReadingState.Turn(args.delta > 0 ? 1 : -1);
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
