using System.Text.Json;
using Xunit;

namespace AstraTerra.Tests.Smoke;

public sealed class ConstellationBookPatchTests
{
    [Fact]
    public void Writable_Vanilla_Books_Are_Enabled_For_The_Offhand()
    {
        var patchPath = Path.Combine(
            RepositoryRoot,
            "assets",
            "astraterra",
            "patches",
            "book-normal-offhand.json");

        using var stream = File.OpenRead(patchPath);
        using var document = JsonDocument.Parse(stream);
        var patch = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal("game:itemtypes/lore/book", patch.GetProperty("file").GetString());
        Assert.Equal("add", patch.GetProperty("op").GetString());
        Assert.Equal("/storageFlagsByType", patch.GetProperty("path").GetString());
        Assert.Equal(
            257,
            patch.GetProperty("value").GetProperty("book-normal-*").GetInt32());
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
