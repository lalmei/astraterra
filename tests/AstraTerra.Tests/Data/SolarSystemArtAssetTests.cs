using AstraTerra.Astronomy;
using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Data;

/// <summary>
/// The pixel art has to answer for every body the catalogue names, and answer with pictures cut to
/// the same convention as the photographs it stands in for. A body whose picture is missing is a
/// body that silently leaves the sky, and one whose picture is not square draws stretched.
/// </summary>
public sealed class SolarSystemArtAssetTests
{
    [Fact]
    public void Every_Catalogued_Body_Has_A_Pixel_Art_Picture()
    {
        foreach (var texturePath in CataloguedTexturePaths())
        {
            var pixelArt = SolarSystemTextures.Resolve(texturePath, SolarSystemArtStyle.Pixel);
            Assert.NotEqual(texturePath, pixelArt);
            Assert.True(File.Exists(FileFor(pixelArt)), $"{texturePath} has no pixel art at {FileFor(pixelArt)}.");
        }
    }

    [Fact]
    public void Every_Moon_Phase_Has_A_Pixel_Art_Face()
    {
        foreach (var face in MoonDiscModel.Faces)
        {
            var pixelArt = SolarSystemTextures.Resolve(face.TexturePath, SolarSystemArtStyle.Pixel);
            Assert.NotEqual(face.TexturePath, pixelArt);
            Assert.True(File.Exists(FileFor(pixelArt)), $"{face.DisplayName} has no pixel art at {FileFor(pixelArt)}.");
        }
    }

    /// <summary>
    /// Square, like the photographs: the quad the renderer builds is square, so the picture's own
    /// width is the body's image width and anything else draws the body squashed or stretched.
    /// </summary>
    [Fact]
    public void Every_Pixel_Art_Picture_Is_Square()
    {
        foreach (var texturePath in SolarSystemTextures.PixelArtTexturePaths)
        {
            var file = FileFor(texturePath);
            Assert.True(File.Exists(file), $"The pixel art set is missing {file}.");
            Assert.Equal(PngDimension(file, offset: 16), PngDimension(file, offset: 20));
        }
    }

    /// <summary>
    /// The eight pixel faces are one moon, so they are all the same square — a face that arrived at
    /// another size would make the moon jump as the month turned.
    /// </summary>
    [Fact]
    public void The_Pixel_Moon_Keeps_One_Size_Through_The_Month()
    {
        var widths = MoonDiscModel.Faces
            .Select(face => FileFor(SolarSystemTextures.Resolve(face.TexturePath, SolarSystemArtStyle.Pixel)))
            .Select(file => PngDimension(file, offset: 16))
            .Distinct();

        Assert.Single(widths);
    }

    /// <summary>Asking for the photographs leaves the catalogue's own picture alone.</summary>
    [Fact]
    public void The_Photographs_Are_The_Catalogue_Untouched()
    {
        foreach (var texturePath in CataloguedTexturePaths())
        {
            Assert.Equal(texturePath, SolarSystemTextures.Resolve(texturePath, SolarSystemArtStyle.Photo));
        }
    }

    /// <summary>A body the pixel art has never heard of keeps the picture the catalogue gave it.</summary>
    [Fact]
    public void An_Unknown_Body_Keeps_Its_Own_Picture()
    {
        Assert.Equal(
            "astraterra:environment/solar-system/uranus-0",
            SolarSystemTextures.Resolve("astraterra:environment/solar-system/uranus-0", SolarSystemArtStyle.Pixel));
    }

    private static IEnumerable<string> CataloguedTexturePaths()
    {
        var catalog = PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));
        foreach (var planet in catalog.Planets)
        {
            foreach (var face in planet.Disc?.Faces ?? [])
            {
                yield return face.TexturePath;
            }

            foreach (var moon in planet.Moons ?? [])
            {
                yield return moon.TexturePath;
            }
        }
    }

    private static string FileFor(string texturePath)
        => Path.Combine(
            "assets/astraterra/textures",
            texturePath.Replace("astraterra:", string.Empty, StringComparison.Ordinal) + ".png");

    /// <summary>A PNG's width and height are big-endian at fixed offsets in its header.</summary>
    private static int PngDimension(string file, int offset)
    {
        var header = new byte[24];
        using var stream = File.OpenRead(file);
        Assert.Equal(header.Length, stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false));
        return (header[offset] << 24) | (header[offset + 1] << 16) | (header[offset + 2] << 8) | header[offset + 3];
    }
}
