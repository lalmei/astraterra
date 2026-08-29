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

    /// <summary>
    /// The render path holds one list for the whole session, so projecting into it has to leave the
    /// same content as the allocating call, and has to leave behind nothing from the projection
    /// before it.
    /// </summary>
    [Fact]
    public void ProjectVisibleObjects_Into_A_Reused_List_Matches_The_Allocating_Call()
    {
        var objects = new[]
        {
            PlateAt("BRIGHT", rightAscensionDeg: 0, declinationDeg: 10, brightness: 0.9),
            PlateAt("FAINT", rightAscensionDeg: 20, declinationDeg: 15, brightness: 0.2),
            PlateAt("BELOW", rightAscensionDeg: 180, declinationDeg: -80, brightness: 0.9)
        };

        var expected = DeepSkyRenderModel.ProjectVisibleObjects(objects, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);

        var destination = new List<RenderedDeepSkyObject>();
        DeepSkyRenderModel.ProjectVisibleObjects(objects, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1, destination);

        Assert.Equal(expected.Select(plate => plate.Id), destination.Select(plate => plate.Id));

        // Reused rather than appended to, and reused again for a sky that has none.
        DeepSkyRenderModel.ProjectVisibleObjects(objects, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1, destination);
        Assert.Equal(expected.Count, destination.Count);

        DeepSkyRenderModel.ProjectVisibleObjects([], latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1, destination);
        Assert.Empty(destination);
    }

    /// <summary>
    /// Plates are alpha blended over one another, so unlike the additive star batches their order is
    /// visible where two overlap, and an order that varied between frames would flicker.
    /// </summary>
    [Fact]
    public void ProjectVisibleObjects_Draws_Dimmest_First_And_Breaks_Ties_By_Id()
    {
        var objects = new[]
        {
            PlateAt("B", rightAscensionDeg: 0, declinationDeg: 10, brightness: 0.5),
            PlateAt("A", rightAscensionDeg: 5, declinationDeg: 10, brightness: 0.5),
            PlateAt("C", rightAscensionDeg: 10, declinationDeg: 10, brightness: 0.2)
        };

        var destination = new List<RenderedDeepSkyObject>();
        DeepSkyRenderModel.ProjectVisibleObjects(objects, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1, destination);

        Assert.Equal(["C", "A", "B"], destination.Select(plate => plate.Id));
    }

    [Fact]
    public void ProjectVisibleObjects_Rejects_A_Missing_Destination()
        => Assert.Throws<ArgumentNullException>(
            () => DeepSkyRenderModel.ProjectVisibleObjects([], 0, 0, 1, destination: null!));

    private static DeepSkyObjectEntry PlateAt(string id, double rightAscensionDeg, double declinationDeg, double brightness)
        => new(
            id,
            id,
            rightAscensionDeg,
            declinationDeg,
            2.0,
            [
                new EquatorialCoordinates(rightAscensionDeg - 1, declinationDeg - 1),
                new EquatorialCoordinates(rightAscensionDeg + 1, declinationDeg - 1),
                new EquatorialCoordinates(rightAscensionDeg + 1, declinationDeg + 1),
                new EquatorialCoordinates(rightAscensionDeg - 1, declinationDeg + 1)
            ],
            brightness,
            1f,
            1f,
            1f,
            "astraterra:environment/deep-sky/stellarium/" + id,
            []);
}
