using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class MoonPhaseAssetTests
{
    /// <summary>
    /// Every face has a picture, and all eight are the same square: the moon is one body, and a face
    /// that arrives at a different size or off its own centre would make it jump as the month turns.
    /// </summary>
    [Fact]
    public void Every_Moon_Phase_Ships_A_Picture_Of_The_Same_Size()
    {
        var sizes = new List<(string Id, int Width, int Height)>();

        foreach (var face in MoonDiscModel.Faces)
        {
            var file = Path.Combine(
                "assets/astraterra/textures",
                face.TexturePath.Replace("astraterra:", string.Empty, StringComparison.Ordinal) + ".png");
            Assert.True(File.Exists(file), $"{face.DisplayName} has no picture at {file}.");
            sizes.Add((face.Id, PngWidth(file), PngHeight(file)));
        }

        Assert.All(sizes, size => Assert.Equal(size.Width, size.Height));
        Assert.Single(sizes.Select(size => size.Width).Distinct());
    }

    private static int PngWidth(string file) => ReadPngDimension(file, offset: 16);

    private static int PngHeight(string file) => ReadPngDimension(file, offset: 20);

    /// <summary>
    /// A PNG's width and height are big-endian at fixed offsets in its header, which is all this
    /// needs and is far less than an image library.
    /// </summary>
    private static int ReadPngDimension(string file, int offset)
    {
        var header = new byte[24];
        using var stream = File.OpenRead(file);
        Assert.Equal(header.Length, stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false));
        return (header[offset] << 24) | (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
    }
}
