using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using AstraTerra;
using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Smoke;

public sealed class BootstrapSmokeTests
{
    [Fact]
    public void Modinfo_Exists_At_Repository_Root()
    {
        Assert.True(File.Exists("modinfo.json"));
    }

    [Fact]
    public void Runtime_Version_Stays_In_Sync_With_Modinfo()
    {
        using var stream = File.OpenRead(Path.Combine(RepositoryRoot, "modinfo.json"));
        using var document = JsonDocument.Parse(stream);
        var modinfoVersion = document.RootElement.GetProperty("version").GetString();

        Assert.NotNull(modinfoVersion);
        Assert.Matches(@"^\d+\.\d+\.\d+$", modinfoVersion);
        Assert.Equal(AstraTerraModMetadata.Version, modinfoVersion);
        Assert.Equal($"AstraTerra mod loaded: version={modinfoVersion}", AstraTerraModMetadata.StartupLogMessage);
    }

    [Fact]
    public void Makefile_Exposes_Version_Bump_Target()
    {
        var makefile = File.ReadAllText(Path.Combine(RepositoryRoot, "Makefile"));

        Assert.Contains("bump-version:", makefile);
        Assert.Contains("bump-patch-version:", makefile);
        Assert.Matches(new Regex(@"make bump-version\s+VERSION=0\.1\.2"), makefile);
        Assert.Matches(new Regex(@"make bump-patch-version\s+Increment patch version"), makefile);
        Assert.DoesNotContain(@"Application\ Support", makefile);
    }

    [Fact]
    public void Startup_Version_Log_Uses_Load_Screen_Event_Channel()
    {
        var modSystem = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/AstraTerraModSystem.cs"));

        Assert.Contains("api.Logger.Event(AstraTerraModMetadata.StartupLogMessage)", modSystem);
    }

    [Fact]
    public void Startup_Logs_Client_Render_Diagnostics()
    {
        var modSystem = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/AstraTerraModSystem.cs"));

        Assert.Contains("api.Logger.Event", modSystem);
        Assert.Contains("AstraTerra startup step: item class registered", modSystem);
        Assert.Contains("AstraTerra startup step: config loaded", modSystem);
        Assert.Contains("AstraTerra startup step: astronomy catalog loaded", modSystem);
        Assert.Contains("AstraTerra startup step: meteor shower catalog loaded", modSystem);
        Assert.Contains("AstraTerra startup step: planet catalog loaded", modSystem);
        Assert.Contains("AstraTerra startup step: telescope zoom patched", modSystem);
        Assert.Contains("AstraTerra startup step: lie-down pose registered", modSystem);
        Assert.Contains("AstraTerra startup step: observation input registered", modSystem);
        Assert.Contains("skyLyingController.Start(api)", modSystem);
        Assert.Contains("SkyLyingState.Reset()", modSystem);
        Assert.Contains("AstraTerra startup step: legacy constellation journal loaded", modSystem);
        Assert.Contains("AstraTerra startup step: client commands registered", modSystem);
        Assert.Contains("AstraTerra startup step: client renderers registered", modSystem);
        Assert.Contains("AstraTerraOverlayMatrixCapture", modSystem);
        Assert.Contains("AstraTerraAstrolabePlanner", modSystem);
        Assert.Contains("AstraTerraSkyCoordinateGrid", modSystem);
        Assert.Contains("AstraTerra.Items.ItemAstrolabe", modSystem);
        Assert.Contains("api.RegisterItemClass(\"AstraTerra.Items.ItemSkyDisc\", typeof(ItemSkyDisc))", modSystem);
        Assert.Contains("SkyDiscReadingState.Reset()", modSystem);

        // The wheel reaches the disc before the hotbar does, and only while the disc is up.
        Assert.Contains("SkyDiscReadingState.Turn(args.delta > 0 ? 1 : -1)", modSystem);
        Assert.Contains("api.Event.MouseWheelMove += OnMouseWheelMove", modSystem);

        // A figure is cut into the disc by the server, so both halves of that have to be wired up:
        // without the client half the disc can never ask, and without the server half nothing is
        // ever written down.
        Assert.Contains("skyDiscEngraveClient = new SkyDiscEngraveClient(api)", modSystem);
        Assert.Contains("skyDiscEngraveClient.Register()", modSystem);
        Assert.Contains("new SkyDiscEngraveServer().Register(api)", modSystem);
        Assert.Contains(
            "new ConstellationOverlayRenderer(api, config, catalog, constellationBookClient, skyDiscEngraveClient)",
            modSystem);
        Assert.Contains("SkyStarSunMoonRenderer.Reset()", modSystem);
        Assert.Contains("SkyStarSunMoonRenderer.Initialize(api, config, catalog, meteorShowers, planets, comets)", modSystem);
        Assert.Contains("AstraTerra startup step: client renderers skipped", modSystem);
    }

    [Fact]
    public void A_Disc_Is_Drawn_On_While_It_Is_Up_And_Its_Figure_Is_Shown_While_It_Is_Held()
    {
        var overlay = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/ConstellationOverlayRenderer.cs"));

        // Raised is what it takes to cut a line.
        Assert.Contains("SkyDiscReadingState.IsReading && SkyDiscHeld.IsHolding", overlay);

        // A line drawn through the disc goes into the disc, not into the book on the other hand.
        Assert.Contains("discClient.SendEngrave(start, end)", overlay);

        // And a disc asks for no book, no ink and no quill.
        Assert.Contains("surface == DrawingSurface.Telescope && !bookClient.CanMutate", overlay);

        // The segments this pass builds are what a telescope points at to rename or unpick, and both
        // of those act on the book by id. A disc's figure among them is a line the book is asked to
        // edit and does not have, so this pass must only ever see the book's own constellations.
        Assert.DoesNotContain("figures.Add(", overlay);
        Assert.Contains("bookClient.ReadCurrentJournalOrEmpty().Constellations", overlay);

        // The figure itself is drawn by the sky, not here — this pass only aims and previews. Wiring
        // it up here instead is exactly the bug where a disc could be drawn on but never showed.
        var sky = File.ReadAllText(
            Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("ReadDiscFigureCached(SkyDiscHeld.Find(api.World.Player))", sky);
        Assert.Contains("ConstellationRecords.Add(discFigure.AsRecord())", sky);
        Assert.Contains("SkyDiscFigureStore.FigureJsonAttribute", sky);
    }

    [Fact]
    public void Telescope_Scope_Renderer_Uses_Telescope_Specific_Raster_Overlays()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/TelescopeScopeRenderer.cs"));

        Assert.Contains("textures/gui/telescope-scope.png", renderer);
        Assert.Contains("textures/gui/telescope-scope-precision.png", renderer);
        Assert.Contains("TelescopeScopeState.MaxZoomStep > TelescopeObservationState.MaxZoomStep", renderer);
        Assert.Contains("Precision Telescope scoped view", renderer);
        Assert.DoesNotContain("GetRimTint", renderer);
    }

    [Fact]
    public void Sky_Renderer_Uses_Reference_Sky_Style_SunMoon_Postfix()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("public static void Postfix(SystemRenderSunMoon __instance, float dt)", renderer);
        Assert.Contains("\"quadModelRef\"", renderer);
        Assert.Contains("GlToggleBlend(true, EnumBlendMode.Glow)", renderer);
        Assert.Contains("GLDisableDepthTest()", renderer);
        Assert.Contains("ConstellationRenderModel.BuildSkyLines", renderer);
        Assert.Contains("SkyMarkTexturePath", renderer);
        Assert.Contains("MinimumSkyRenderDarkness", renderer);
        Assert.Contains("MinimumStarAlpha", renderer);
        Assert.Contains("ShouldRenderForDarkness(naturalDarkness, forceDaylightStars)", renderer);
        Assert.Contains("AstraTerra disabled sky rendering after an unexpected error", renderer);
        Assert.Contains("MeteorShowerActivity.ReadAll", renderer);
        Assert.Contains("MeteorStreakMeshBuilder.Build", renderer);
        Assert.Contains("RenderMeteorStreaks", renderer);
    }

    [Fact]
    public void Sky_Renderer_Reuses_Model_Matrix_Buffer_Per_Frame()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("var modelMatrixBuffer = new float[16];", renderer);
        Assert.Contains("CopyToFloatArray(modelMatrix, modelMatrixBuffer)", renderer);
        Assert.DoesNotContain("ToFloatArray(modelMatrix)", renderer);
    }

    [Fact]
    public void Telescope_Deep_Sky_Plates_Render_In_Front_Of_Catalog_Stars()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        var starsIndex = renderer.IndexOf("foreach (var star in ", StringComparison.Ordinal);
        var foregroundBlendIndex = renderer.IndexOf("render.GlToggleBlend(true, EnumBlendMode.Standard);", starsIndex, StringComparison.Ordinal);
        var deepSkyIndex = renderer.IndexOf("foreach (var deepSkyObject in visibleDeepSkyObjects)", foregroundBlendIndex, StringComparison.Ordinal);
        // The constellation marks are one batched draw now, a ribbon per edge.
        var constellationIndex = renderer.IndexOf("RenderConstellationLines(clientApi, shader, starResidualRotation, modelMatrixBuffer);", deepSkyIndex, StringComparison.Ordinal);

        Assert.True(starsIndex >= 0);
        Assert.True(foregroundBlendIndex > starsIndex);
        Assert.True(deepSkyIndex > foregroundBlendIndex);
        Assert.True(constellationIndex > deepSkyIndex);
    }

    /// <summary>
    /// <c>Vec4f</c> is a class in the Vintage Story API, so a tint built per body per frame is
    /// thousands of heap objects a second on the render thread. Star and planet colour now rides on
    /// mesh vertices instead; this fails if a per-body <c>Vec4f</c> creeps back into the batch build.
    /// </summary>
    [Fact]
    public void The_Star_And_Planet_Batch_Does_Not_Allocate_A_Tint_Per_Body()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        var batchIndex = renderer.IndexOf("private static void EnsureStarMeshes(", StringComparison.Ordinal);
        var batchEndIndex = renderer.IndexOf("private static SkyBillboard Billboard(", batchIndex, StringComparison.Ordinal);

        Assert.True(batchIndex >= 0);
        Assert.True(batchEndIndex > batchIndex);
        Assert.DoesNotContain("new Vec4f(", renderer[batchIndex..batchEndIndex], StringComparison.Ordinal);
    }

    /// <summary>
    /// Around three thousand stars used to be three thousand draw calls. They are now grouped by the
    /// sprite they use, which is at most a handful of batches.
    /// </summary>
    [Fact]
    public void Stars_Are_Drawn_As_Batches_Rather_Than_One_Quad_Each()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("DrawBillboardBatch(clientApi, shader, brightStarMesh", renderer, StringComparison.Ordinal);
        Assert.Contains("DrawBillboardBatch(clientApi, shader, faintStarMesh", renderer, StringComparison.Ordinal);
        Assert.Contains("DrawBillboardBatch(clientApi, shader, planetMesh", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderStarQuad(", renderer, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sky is geometry the weather cannot reach. Feeding it the scene's fog let a mod that keeps
    /// a bright fog colour after sunset replace the whole starfield with a flat sheet of it, which
    /// is what a player reported as deep-sky plates with broken transparency.
    /// </summary>
    [Fact]
    public void The_Sky_Pass_Is_Drawn_Without_Scene_Fog()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("shader.FogDensityIn = 0f;", renderer, StringComparison.Ordinal);
        Assert.Contains("shader.FogMinIn = 0f;", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("render.FogColor", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("render.FogDensity", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("render.FogMin", renderer, StringComparison.Ordinal);
    }

    /// <summary>
    /// Deep-sky plates used to rebuild and re-upload their geometry into one shared buffer once per
    /// plate per frame, which cost ten times the sky pass and produced 200 ms hitches.
    /// </summary>
    [Fact]
    public void Deep_Sky_Plates_Keep_Their_Geometry_Between_Frames()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        // One buffer per plate, rebuilt behind a dirty flag rather than on every draw.
        Assert.Contains("EnsureDeepSkyPlateMeshes(clientApi, visibleDeepSkyObjects);", renderer, StringComparison.Ordinal);
        Assert.Contains("if (!deepSkyPlateMeshesDirty)", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("deepSkyQuadMesh", renderer, StringComparison.Ordinal);

        // And a projection held to the same threshold as the stars, not redone per frame.
        Assert.Contains("EnsureProjectedDeepSkyObjects(", renderer, StringComparison.Ordinal);
        Assert.Contains("DeepSkyRenderModel.ProjectVisibleObjects(objects, latitudeDeg, localSiderealDeg, brightnessBias, ProjectedDeepSkyObjects);", renderer, StringComparison.Ordinal);
    }

    /// <summary>
    /// Resolved planets and their moons use the same cached-projection scheme as telescope plates.
    /// Rewriting every little quad on every scoped frame would put the render-thread buffer stalls
    /// back into the path that plate caching just removed.
    /// </summary>
    [Fact]
    public void Resolved_Planet_Discs_Keep_Their_Geometry_Between_Projection_Refreshes()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("private static bool planetDiscMeshesDirty = true;", renderer, StringComparison.Ordinal);
        Assert.Contains("if (!planetDiscMeshesDirty)", renderer, StringComparison.Ordinal);
        Assert.Contains("planetDiscMeshesDirty = true;", renderer, StringComparison.Ordinal);
    }

    /// <summary>
    /// A replacement catalog is a different solar system. Both the naked-eye model and the resolved
    /// telescope model have to release their derived state, including across client sessions whose
    /// calendars happen to use the same number of days per year.
    /// </summary>
    [Fact]
    public void Planet_Catalog_Replacement_And_Reset_Clear_The_Resolved_Disc_Model()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));
        var replacementStart = renderer.IndexOf("public static void ReplacePlanetCatalog(", StringComparison.Ordinal);
        var replacementEnd = renderer.IndexOf("public static void ReplaceCometCatalog(", replacementStart, StringComparison.Ordinal);
        var resetStart = renderer.IndexOf("public static void Reset()", StringComparison.Ordinal);
        var resetEnd = renderer.IndexOf("public static string SetForceDaylightStars", resetStart, StringComparison.Ordinal);

        Assert.True(replacementStart >= 0 && replacementEnd > replacementStart);
        Assert.True(resetStart >= 0 && resetEnd > resetStart);
        Assert.Contains("planetDiscModel = null;", renderer[replacementStart..replacementEnd], StringComparison.Ordinal);
        Assert.Contains("ProjectedPlanetDiscs.Clear();", renderer[replacementStart..replacementEnd], StringComparison.Ordinal);
        Assert.Contains("planetDiscModel = null;", renderer[resetStart..resetEnd], StringComparison.Ordinal);
        Assert.Contains("planetDiscModelDaysPerYear = 0;", renderer[resetStart..resetEnd], StringComparison.Ordinal);
    }

    /// <summary>Sky geometry is beyond the weather, whichever pass owns it.</summary>
    [Fact]
    public void Moon_And_Near_Body_Passes_Are_Drawn_Without_Scene_Fog()
    {
        foreach (var file in new[] { "MoonDiscRenderer.cs", "NearBodyRenderer.cs" })
        {
            var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering", file));

            Assert.Contains("shader.RgbaFogIn = NoFog;", renderer, StringComparison.Ordinal);
            Assert.Contains("shader.FogMinIn = 0f;", renderer, StringComparison.Ordinal);
            Assert.Contains("shader.FogDensityIn = 0f;", renderer, StringComparison.Ordinal);
            Assert.DoesNotContain("render.FogColor", renderer, StringComparison.Ordinal);
            Assert.DoesNotContain("render.FogMin", renderer, StringComparison.Ordinal);
            Assert.DoesNotContain("render.FogDensity", renderer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_Near_Body_Render_Failure_Restores_The_Vanilla_Moon()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/NearBodyRenderer.cs"));
        var catchStart = renderer.IndexOf("catch (Exception exception)", StringComparison.Ordinal);
        var catchEnd = renderer.IndexOf("public void Dispose()", catchStart, StringComparison.Ordinal);

        Assert.True(catchStart >= 0 && catchEnd > catchStart);
        Assert.Contains("renderingDisabledAfterFailure = true;", renderer[catchStart..catchEnd], StringComparison.Ordinal);
        Assert.Contains("HidesVanillaMoon = false;", renderer[catchStart..catchEnd], StringComparison.Ordinal);
    }

    [Fact]
    public void Sky_Renderer_Reset_Clears_Static_Session_State_In_Source()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("public static void Reset()", renderer);
        Assert.Contains("api = null;", renderer);
        Assert.Contains("config = null;", renderer);
        Assert.Contains("catalog = null;", renderer);
        Assert.Contains("meteorShowers = Array.Empty<MeteorShowerEntry>();", renderer);
        Assert.Contains("meteorVisuals.Clear();", renderer);
        Assert.Contains("forceDaylightStars = false;", renderer);
        Assert.Contains("renderingDisabledAfterFailure = false;", renderer);
    }

    [Fact]
    public void English_Lang_File_Exists()
    {
        Assert.True(File.Exists("assets/astraterra/lang/en.json"));
    }

    [Fact]
    public void Vanilla_Starfield_Assets_Are_Not_Overridden()
    {
        var vanillaFaces = new[]
        {
            "stars-bg.png",
            "stars-dn.png",
            "stars-ft.png",
            "stars-lf.png",
            "stars-rt.png",
            "stars-up.png"
        };

        foreach (var face in vanillaFaces)
        {
            Assert.False(
                File.Exists(Path.Combine(RepositoryRoot, "assets/game/textures/environment", face)),
                $"AstraTerra must not shadow vanilla starfield face {face}.");
        }
    }

    [Fact]
    public void Config_Class_Uses_The_Agreed_V1_Fields()
    {
        var props = typeof(AstraTerraConfig).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("StarfieldMode", props);
        Assert.Contains("SkyGridMode", props);
        Assert.Contains("StarBrightnessBias", props);
        Assert.Contains("GuideStarHighlightStrength", props);
        Assert.Contains("SelectionSnapRadiusDeg", props);
        Assert.Contains("ShowMinimalHud", props);
        Assert.Contains("ShowReticle", props);
        Assert.Contains("DebugGuideStarEmphasisDefault", props);
        Assert.Contains("DebugMeteorRateMultiplier", props);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
