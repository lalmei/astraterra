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
        Assert.Contains("AstraTerra startup step: telescope zoom patched", modSystem);
        Assert.Contains("AstraTerra startup step: observation input registered", modSystem);
        Assert.Contains("AstraTerra startup step: legacy constellation journal loaded", modSystem);
        Assert.Contains("AstraTerra startup step: client commands registered", modSystem);
        Assert.Contains("AstraTerra startup step: client renderers registered", modSystem);
        Assert.Contains("AstraTerraOverlayMatrixCapture", modSystem);
        Assert.Contains("AstraTerraAstrolabePlanner", modSystem);
        Assert.Contains("AstraTerraSkyCoordinateGrid", modSystem);
        Assert.Contains("AstraTerra.Items.ItemAstrolabe", modSystem);
        Assert.Contains("SkyStarSunMoonRenderer.Reset()", modSystem);
        Assert.Contains("SkyStarSunMoonRenderer.Initialize(api, config, catalog)", modSystem);
        Assert.Contains("AstraTerra startup step: client renderers skipped", modSystem);
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
        Assert.Contains("AstraTerra disabled sky rendering after an unexpected error", renderer);
    }

    [Fact]
    public void Sky_Renderer_Reset_Clears_Static_Session_State_In_Source()
    {
        var renderer = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/Client/Rendering/SkyStarSunMoonRenderer.cs"));

        Assert.Contains("public static void Reset()", renderer);
        Assert.Contains("api = null;", renderer);
        Assert.Contains("config = null;", renderer);
        Assert.Contains("catalog = null;", renderer);
        Assert.Contains("forceDaylightStars = false;", renderer);
        Assert.Contains("renderingDisabledAfterFailure = false;", renderer);
    }

    [Fact]
    public void English_Lang_File_Exists()
    {
        Assert.True(File.Exists("assets/astraterra/lang/en.json"));
    }

    [Fact]
    public void Config_Class_Uses_The_Agreed_V1_Fields()
    {
        var props = typeof(AstraTerraConfig).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Contains("SkyGridMode", props);
        Assert.Contains("StarBrightnessBias", props);
        Assert.Contains("GuideStarHighlightStrength", props);
        Assert.Contains("SelectionSnapRadiusDeg", props);
        Assert.Contains("ShowMinimalHud", props);
        Assert.Contains("ShowReticle", props);
        Assert.Contains("DebugGuideStarEmphasisDefault", props);
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
