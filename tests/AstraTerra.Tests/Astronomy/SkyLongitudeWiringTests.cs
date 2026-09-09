using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// No unit test can reach the game's calendar, so the guard that keeps the star field, the
/// instruments and the displayed clock on the same longitude as the visible sun is the fact that
/// none of them map longitude themselves. Only the sun's own wrapper does.
/// </summary>
public sealed class SkyLongitudeWiringTests
{
    [Fact]
    public void Only_The_Sun_Wrapper_Maps_Longitude_Without_Asking_Whether_The_Sun_Follows_It()
    {
        var offenders = EnumerateSources()
            .Where(file => File.ReadAllText(file).Contains(
                "LatitudeMapper.MapWorldLongitude",
                StringComparison.Ordinal))
            .Select(file => Path.GetFileName(file) ?? file)
            .Order()
            .ToArray();

        Assert.Equal(["LongitudeAwareSunInstaller.cs", "ObserverLongitude.cs"], offenders);
    }

    /// <summary>
    /// Both moon reads have to go through <c>LocalMoonTime</c>, or the moon quietly goes back to
    /// universal time while everything drawn around it stays on the observer's.
    /// </summary>
    [Theory]
    [InlineData("Client", "Rendering", "MoonDiscRenderer.cs")]
    [InlineData("Client", "Rendering", "SextantReadingRenderer.cs")]
    public void The_Moon_Is_Never_Read_Straight_Off_The_World_Clock(params string[] relativePath)
    {
        var source = File.ReadAllText(Path.Combine([RepositoryRoot(), "src", "AstraTerra", .. relativePath]));

        Assert.Contains("LocalMoonTime.MoonTotalDays", source);

        // The phase and its brightness belong to the instant the moon is read at, not to now.
        Assert.DoesNotContain("calendar.MoonPhaseExact", source);
        Assert.DoesNotContain("calendar.MoonPhaseBrightness", source);
        Assert.DoesNotContain("GetMoonPosition(position.XYZ, calendar.TotalDays)", source);
        Assert.DoesNotContain("GetMoonPosition(entity.Pos.XYZ, calendar.TotalDays)", source);
    }

    /// <summary>
    /// Near bodies are the only things in this sky placed by hour angle rather than right ascension,
    /// and that makes them the only ones that need the observer's longitude handed over separately.
    /// The sidereal angle alone cancels it against itself and pins the parent giant to the player's
    /// own sky, so the extra argument is the whole fix and nothing checks it but this.
    /// </summary>
    [Fact]
    public void Near_Bodies_Are_Placed_With_The_Observers_Longitude_As_Well_As_Its_Sidereal_Angle()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "AstraTerra",
            "Client",
            "Rendering",
            "NearBodyRenderer.cs"));

        Assert.Contains("ObserverLongitude.ForObserver", source);

        var call = source[source.IndexOf("NearBodyRenderModel.Place(", StringComparison.Ordinal)..];
        call = call[..call.IndexOf(");", StringComparison.Ordinal)];
        Assert.Contains("longitude", call);
    }

    private static IEnumerable<string> EnumerateSources()
        => Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot(), "src", "AstraTerra"),
            "*.cs",
            SearchOption.AllDirectories);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
