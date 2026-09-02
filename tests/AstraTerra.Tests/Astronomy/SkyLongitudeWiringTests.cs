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

    private static IEnumerable<string> EnumerateSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        return Directory.EnumerateFiles(Path.Combine(root, "src", "AstraTerra"), "*.cs", SearchOption.AllDirectories);
    }
}
