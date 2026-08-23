using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class MilkyWayRenderModelTests
{
    [Fact]
    public void Every_Vertex_Points_Somewhere_On_The_Sky_Sphere()
    {
        var band = MilkyWayRenderModel.ProjectBand(latitudeDeg: 45, localSiderealDeg: 120);

        Assert.All(
            band.Vertices,
            vertex => Assert.Equal(
                1.0,
                Math.Sqrt(
                    (vertex.DirectionX * vertex.DirectionX) +
                    (vertex.DirectionY * vertex.DirectionY) +
                    (vertex.DirectionZ * vertex.DirectionZ)),
                precision: 6));
    }

    [Fact]
    public void The_Seam_Column_Repeats_The_First_One_At_The_Far_Edge_Of_The_Map()
    {
        var band = MilkyWayRenderModel.ProjectBand(
            latitudeDeg: 0,
            localSiderealDeg: 0,
            longitudeSteps: 8,
            latitudeSteps: 4);

        var first = band.Vertices[0];
        var last = band.Vertices[8];

        // Same direction, opposite ends of the texture: wrapping back to u = 0 instead would run the
        // whole glow map backwards across the last cell.
        Assert.Equal(first.DirectionX, last.DirectionX, precision: 9);
        Assert.Equal(first.DirectionY, last.DirectionY, precision: 9);
        Assert.Equal(first.DirectionZ, last.DirectionZ, precision: 9);
        Assert.Equal(0f, first.U);
        Assert.Equal(1f, last.U);
    }

    [Fact]
    public void The_Map_Runs_From_Pole_To_Pole_Down_The_Texture()
    {
        var band = MilkyWayRenderModel.ProjectBand(
            latitudeDeg: 0,
            localSiderealDeg: 0,
            longitudeSteps: 8,
            latitudeSteps: 4);

        Assert.Equal(0f, band.Vertices[0].V);
        Assert.Equal(1f, band.Vertices[^1].V);
    }

    [Fact]
    public void Nothing_Below_The_Horizon_Is_Drawn()
    {
        var band = MilkyWayRenderModel.ProjectBand(latitudeDeg: 30, localSiderealDeg: 200);

        var below = band.Vertices.Where(vertex => vertex.DirectionY < -0.1).ToList();

        Assert.NotEmpty(below);
        Assert.All(below, vertex => Assert.Equal(0f, vertex.Alpha));
    }

    [Fact]
    public void The_Band_Fades_In_Rather_Than_Appearing_At_Full_Strength()
    {
        var band = MilkyWayRenderModel.ProjectBand(latitudeDeg: 30, localSiderealDeg: 200);

        // Somewhere between the cutoff and the fade band there has to be a partly drawn vertex, or
        // the band's edge is a hard line lying across the hills.
        Assert.Contains(band.Vertices, vertex => vertex.Alpha > 0f && vertex.Alpha < 1f);
        Assert.Contains(band.Vertices, vertex => vertex.Alpha >= 1f);
    }

    [Fact]
    public void Cells_Wholly_Below_The_Horizon_Are_Left_Out_But_Still_Reserved()
    {
        var band = MilkyWayRenderModel.ProjectBand(
            latitudeDeg: 15,
            localSiderealDeg: 80,
            longitudeSteps: 16,
            latitudeSteps: 8);

        Assert.Equal(16 * 8 * 6, band.IndexCapacity);
        Assert.NotEmpty(band.Indices);
        Assert.True(
            band.Indices.Count < band.IndexCapacity,
            "About half the sphere is under the observer at any moment, so some cells must be skipped.");
    }

    [Fact]
    public void A_Grid_Too_Coarse_To_Be_A_Sphere_Is_Refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MilkyWayRenderModel.ProjectBand(0, 0, longitudeSteps: 3, latitudeSteps: 4));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MilkyWayRenderModel.ProjectBand(0, 0, longitudeSteps: 8, latitudeSteps: 1));
    }
}
