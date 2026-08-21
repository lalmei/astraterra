using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class DeepSkyRenderModelTests
{
    [Fact]
    public void Project_Returns_Visible_Object_Above_Horizon()
    {
        var entry = new DeepSkyObjectEntry(
            "TEST",
            "Test Nebula",
            0,
            0,
            2.0,
            [
                new EquatorialCoordinates(-1, -1),
                new EquatorialCoordinates(1, -1),
                new EquatorialCoordinates(1, 1),
                new EquatorialCoordinates(-1, 1)
            ],
            0.5,
            0.8f,
            0.9f,
            1.0f,
            "astraterra:environment/deep-sky/stellarium/test",
            ["astraterra:environment/deep-sky-cloud"]);

        var rendered = DeepSkyRenderModel.Project(entry, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.NotNull(rendered);
        Assert.Equal("TEST", rendered!.Id);
        Assert.InRange(rendered.Brightness, 0.49, 0.51);
        Assert.Equal(2.0, rendered.AngularSizeDeg);
        Assert.Equal(4, rendered.QuadCorners.Count);
        Assert.All(
            rendered.QuadCorners,
            corner => Assert.Equal(1.0, Math.Sqrt((corner.X * corner.X) + (corner.Y * corner.Y) + (corner.Z * corner.Z)), precision: 6));
        Assert.Equal(
            ["astraterra:environment/deep-sky/stellarium/test", "astraterra:environment/deep-sky-cloud"],
            rendered.TexturePaths);
    }

    [Fact]
    public void Project_Skips_Object_Below_Horizon()
    {
        var entry = new DeepSkyObjectEntry(
            "TEST",
            "Test Nebula",
            180,
            0,
            2.0,
            [
                new EquatorialCoordinates(179, -1),
                new EquatorialCoordinates(181, -1),
                new EquatorialCoordinates(181, 1),
                new EquatorialCoordinates(179, 1)
            ],
            0.5,
            0.8f,
            0.9f,
            1.0f,
            "astraterra:environment/deep-sky/stellarium/test",
            ["astraterra:environment/deep-sky-cloud"]);

        var rendered = DeepSkyRenderModel.Project(entry, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Null(rendered);
    }

    [Fact]
    public void Project_Preserves_Stellarium_Corner_Order_And_Rotation()
    {
        EquatorialCoordinates[] worldCoords =
        [
            new EquatorialCoordinates(-2, -1),
            new EquatorialCoordinates(1, -2),
            new EquatorialCoordinates(2, 1),
            new EquatorialCoordinates(-1, 2)
        ];
        var entry = new DeepSkyObjectEntry(
            "TEST",
            "Rotated Nebula",
            0,
            0,
            2.0,
            worldCoords,
            0.5,
            1f,
            1f,
            1f,
            "astraterra:environment/deep-sky/stellarium/test",
            []);

        var rendered = DeepSkyRenderModel.Project(entry, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.NotNull(rendered);
        Assert.Equal(4, rendered!.QuadCorners.Distinct().Count());
        for (var index = 0; index < worldCoords.Length; index++)
        {
            var sourceCorner = worldCoords[index];
            var projectedCorner = StarRenderModel.Project(
                new StarCatalogEntry(index, sourceCorner.RightAscensionDeg, sourceCorner.DeclinationDeg, 1, null, false),
                latitudeDeg: 0,
                localSiderealDeg: 0,
                brightnessBias: 1);

            Assert.NotNull(projectedCorner);
            Assert.Equal(projectedCorner!.Value.DirectionX, rendered.QuadCorners[index].X, precision: 12);
            Assert.Equal(projectedCorner.Value.DirectionY, rendered.QuadCorners[index].Y, precision: 12);
            Assert.Equal(projectedCorner.Value.DirectionZ, rendered.QuadCorners[index].Z, precision: 12);
        }
    }
}
