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
        Assert.DoesNotContain("GetMoonPosition(position.XYZ, calendar.TotalDays)", source);
        Assert.DoesNotContain("GetMoonPosition(entity.Pos.XYZ, calendar.TotalDays)", source);
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
