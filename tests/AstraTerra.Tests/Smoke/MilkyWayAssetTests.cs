using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Smoke;

public sealed class MilkyWayAssetTests
{
    /// <summary>
    /// The glow map is loaded by path, and a path that names nothing fails quietly: the band is
    /// simply not drawn, which from the ground looks like a cloudy night. The map changed name when
    /// it became a JPEG, so this pins the configured path to a file that ships.
    /// </summary>
    [Fact]
    public void The_Configured_Milky_Way_Texture_Ships()
    {
        var assetPath = SkyTextureLoader.ToAssetPath(SkyStarSunMoonRenderer.MilkyWayTexturePath);
        var separator = assetPath.IndexOf(':', StringComparison.Ordinal);
        var domain = assetPath[..separator];
        var relative = assetPath[(separator + 1)..];

        var file = Path.Combine([RepositoryRoot, "assets", domain, .. relative.Split('/')]);

        Assert.True(File.Exists(file), $"{SkyStarSunMoonRenderer.MilkyWayTexturePath} resolves to {file}, which does not exist.");
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
