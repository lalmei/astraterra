using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Client.Zoom;
using AstraTerra.Commands;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra;

public sealed class AstraTerraModSystem : ModSystem
{
    private AstraTerraConfig? config;
    private StarCatalog? catalog;
    private TelescopeZoomPatcher? telescopeZoomPatcher;
    private ICoreClientAPI? clientApi;
    private ConstellationOverlayRenderer? constellationOverlayRenderer;
    private ConstellationBookClient? constellationBookClient;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(AstraTerraModMetadata.StartupLogMessage);
        api.RegisterItemClass("AstraTerra.Items.ItemBrassTelescope", typeof(ItemBrassTelescope));
        api.RegisterItemClass("AstraTerra.Items.ItemPrecisionTelescope", typeof(ItemPrecisionTelescope));
        api.RegisterItemClass("AstraTerra.Items.ItemSextant", typeof(ItemSextant));
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemBrassTelescope");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemPrecisionTelescope");
        api.Logger.Event("AstraTerra startup step: item class registered: AstraTerra.Items.ItemSextant");
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api is ICoreClientAPI clientApi)
        {
            config = AstraTerraConfigLoader.Load(clientApi);
            api.Logger.Event(
                "AstraTerra startup step: config loaded: starBrightnessBias={0:0.00}; showMinimalHud={1}; showReticle={2}; debugGuideStarEmphasis={3}",
                config.StarBrightnessBias,
                config.ShowMinimalHud,
                config.ShowReticle,
                config.DebugGuideStarEmphasisDefault);
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
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        SkyStarSunMoonRenderer.Reset();
        clientApi = api;
        config ??= AstraTerraConfigLoader.Load(api);
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
                dayOfYearProvider: () => api.World.Calendar.DayOfYear),
            constellationBookClient).Register(api);
        api.Logger.Event("AstraTerra startup step: client commands registered: .stars list/info/build/connect/name/select/delete/debug/daylight-stars");

        if (catalog is null)
        {
            api.Logger.Event("AstraTerra startup step: client renderers skipped: astronomy catalog was not loaded");
            return;
        }

        SkyStarSunMoonRenderer.Initialize(api, config, catalog);
        constellationOverlayRenderer = new ConstellationOverlayRenderer(api, config, catalog, constellationBookClient);
        api.Event.RegisterRenderer(constellationOverlayRenderer, EnumRenderStage.Opaque, "AstraTerraOverlayMatrixCapture");
        api.Event.RegisterRenderer(constellationOverlayRenderer, EnumRenderStage.Ortho, "AstraTerraOverlay");
        api.Event.MouseDown += constellationOverlayRenderer.OnMouseDown;
        api.Event.MouseMove += constellationOverlayRenderer.OnMouseMove;
        api.Event.MouseUp += constellationOverlayRenderer.OnMouseUp;
        api.Event.RegisterRenderer(new TelescopeScopeRenderer(api), EnumRenderStage.Ortho, "AstraTerraTelescopeScope");
        var sextantReadingRenderer = new SextantReadingRenderer(api, config, catalog);
        api.Event.RegisterRenderer(sextantReadingRenderer, EnumRenderStage.Opaque, "AstraTerraSextantMatrixCapture");
        api.Event.RegisterRenderer(sextantReadingRenderer, EnumRenderStage.Ortho, "AstraTerraSextantReading");
        api.Logger.Event(
            "AstraTerra startup step: client renderers registered: skyPatch=SystemRenderSunMoon.OnRenderFrame3D; overlay=AstraTerraOverlayMatrixCapture+AstraTerraOverlay; telescopeScope=AstraTerraTelescopeScope; sextant=AstraTerraSextantMatrixCapture+AstraTerraSextantReading; stars={0}; guideGroups={1}; skyCultures={2}; deepSkyObjects={3}",
            catalog.Stars.Count,
            catalog.GuideGroups.Count,
            catalog.SkyCultures.Count,
            catalog.DeepSkyObjects.Count);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        new ConstellationBookServer(() => catalog).Register(api);
        new StarsServerCommands(() => catalog).Register(api);
    }

    public override void Dispose()
    {
        telescopeZoomPatcher?.Stop();
        SkyStarSunMoonRenderer.Reset();
        if (clientApi is not null && constellationOverlayRenderer is not null)
        {
            clientApi.Event.MouseDown -= constellationOverlayRenderer.OnMouseDown;
            clientApi.Event.MouseMove -= constellationOverlayRenderer.OnMouseMove;
            clientApi.Event.MouseUp -= constellationOverlayRenderer.OnMouseUp;
        }
    }

    private static void OnMouseWheelMove(MouseWheelEventArgs args)
    {
        if (!Observation.TelescopeScopeState.IsScoped)
        {
            return;
        }

        Observation.TelescopeScopeState.ScrollZoom(args.delta > 0 ? 1 : -1);
        args.SetHandled(true);
    }
}
