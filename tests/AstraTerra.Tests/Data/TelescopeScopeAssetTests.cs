using System.Buffers.Binary;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class TelescopeScopeAssetTests
{
    [Theory]
    [InlineData("telescope-scope.png")]
    [InlineData("telescope-scope-precision.png")]
    public void Telescope_Scope_Overlays_Are_Square_Rgba_Pngs(string fileName)
    {
        var path = Path.Combine("assets/astraterra/textures/gui", fileName);
        var bytes = File.ReadAllBytes(path);

        Assert.True(bytes.Length >= 33, $"{fileName} is too short to contain a PNG header.");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(bytes, 12, 4));

        var width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        var colorType = bytes[25];

        Assert.Equal(width, height);
        Assert.True(width >= 1024, $"{fileName} should retain enough resolution for the full-screen scope.");
        Assert.Equal(6, colorType);
    }
}
