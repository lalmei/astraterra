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
        Assert.Contains("SkyStarSunMoonRenderer.Reset()", modSystem);
        Assert.Contains("SkyStarSunMoonRenderer.Initialize(api, config, catalog, meteorShowers, planets)", modSystem);
        Assert.Contains("AstraTerra startup step: client renderers skipped", modSystem);
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
        Assert.Contains("ConstellationRenderModel.BuildSkyDots", renderer);
        Assert.Contains("ConstellationDotTexturePath", renderer);
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

        var starsIndex = renderer.IndexOf("foreach (var star in visibleStars)", StringComparison.Ordinal);
        var foregroundBlendIndex = renderer.IndexOf("render.GlToggleBlend(true, EnumBlendMode.Standard);", starsIndex, StringComparison.Ordinal);
        var deepSkyIndex = renderer.IndexOf("foreach (var deepSkyObject in visibleDeepSkyObjects)", foregroundBlendIndex, StringComparison.Ordinal);
        // The constellation marks are one batched draw now, not a loop over dots.
        var constellationIndex = renderer.IndexOf("RenderConstellationDots(clientApi, shader, modelMatrixBuffer);", deepSkyIndex, StringComparison.Ordinal);

        Assert.True(starsIndex >= 0);
        Assert.True(foregroundBlendIndex > starsIndex);
        Assert.True(deepSkyIndex > foregroundBlendIndex);
        Assert.True(constellationIndex > deepSkyIndex);
    }

    /// <summary>
    /// <c>Vec4f</c> is a class in the Vintage Story API, so a tint built per body per frame is
    /// thousands of heap objects a second on the render thread. The draw loops keep one instance and
    /// write into it; this fails if a <c>new Vec4f</c> creeps back between the star loop and the end
    /// of the planet loop.
    /// </summary>
    [Fact]
    public void The_Star_And_Planet_Draw_Loops_Do_Not_Allocate_A_Tint_Per_Body()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        var starLoopIndex = renderer.IndexOf("foreach (var star in visibleStars)", StringComparison.Ordinal);
        var planetLoopEndIndex = renderer.IndexOf("if (visibleDeepSkyObjects.Count > 0)", starLoopIndex, StringComparison.Ordinal);

        Assert.True(starLoopIndex >= 0);
        Assert.True(planetLoopEndIndex > starLoopIndex);
        Assert.DoesNotContain("new Vec4f(", renderer[starLoopIndex..planetLoopEndIndex], StringComparison.Ordinal);
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
