using System;
using System.IO;
using Xunit;

namespace AstraTerra.Tests.Smoke;

/// <summary>
/// Guards the one rule that keeps shutdown from taking the process down: nothing holding an
/// uploaded mesh or texture may be released from the side-agnostic half of
/// <c>AstraTerraModSystem.Dispose</c>.
/// </summary>
/// <remarks>
/// In singleplayer both sides run in one process, so a static is shared between them, and the
/// server's mod loader is disposed first on a thread with no GL context. Releasing a mesh there
/// reaches glDeleteBuffers through a null dispatch: a segfault, not a managed exception, so no
/// crash log is written and the world save that was still to come never happens. That is what a
/// player sees as "the game crashes when I save the world".
/// </remarks>
public sealed class DisposeSideSafetyTests
{
    [Fact]
    public void Static_Renderer_State_Is_Released_Only_Behind_The_Client_Guard()
    {
        var dispose = DisposeBody();
        var guard = dispose.IndexOf("if (clientApi is null)", StringComparison.Ordinal);
        var reset = dispose.IndexOf("SkyStarSunMoonRenderer.Reset()", StringComparison.Ordinal);

        Assert.True(guard >= 0, "Dispose must return early when there is no client API.");
        Assert.True(reset >= 0, "Dispose must still release the renderer's static meshes on a client.");
        Assert.True(reset > guard, "SkyStarSunMoonRenderer.Reset() disposes uploaded meshes and must sit below the client guard.");
    }

    [Fact]
    public void Server_Side_Teardown_Still_Runs_Above_The_Client_Guard()
    {
        var dispose = DisposeBody();
        var guard = dispose.IndexOf("if (clientApi is null)", StringComparison.Ordinal);

        Assert.True(guard >= 0);
        foreach (var serverTeardown in new[]
        {
            "skyDiscFiringPatch.Stop()",
            "serverLongitudeAwareSunInstaller?.Dispose()",
            "serverNearBodyLightInstaller?.Dispose()",
            "FoundSkyDisc.Reset()"
        })
        {
            var index = dispose.IndexOf(serverTeardown, StringComparison.Ordinal);
            Assert.True(index >= 0, $"Dispose no longer performs {serverTeardown}.");
            Assert.True(index < guard, $"{serverTeardown} is server teardown and must run above the client guard.");
        }
    }

    private static string DisposeBody()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src/AstraTerra/AstraTerraModSystem.cs"));
        var start = source.IndexOf("public override void Dispose()", StringComparison.Ordinal);
        Assert.True(start >= 0, "AstraTerraModSystem no longer overrides Dispose.");

        var end = source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        Assert.True(end > start, "Could not read the body of Dispose.");
        return source[start..end];
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
