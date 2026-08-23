using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class MilkyWayBandMeshBuilderTests
{
    [Fact]
    public void The_Mesh_Carries_Every_Grid_Vertex_At_The_Sky_Radius()
    {
        var band = MilkyWayRenderModel.ProjectBand(
            latitudeDeg: 0,
            localSiderealDeg: 0,
            longitudeSteps: 8,
            latitudeSteps: 4);

        var mesh = MilkyWayBandMeshBuilder.Build(band, radius: 40f);

        Assert.Equal(band.Vertices.Count, mesh.VerticesCount);
        for (var index = 0; index < mesh.VerticesCount; index++)
        {
            var offset = index * 3;
            var length = Math.Sqrt(
                (mesh.xyz[offset] * mesh.xyz[offset]) +
                (mesh.xyz[offset + 1] * mesh.xyz[offset + 1]) +
                (mesh.xyz[offset + 2] * mesh.xyz[offset + 2]));
            Assert.Equal(40.0, length, precision: 4);
        }
    }

    [Fact]
    public void The_Index_Buffer_Stays_The_Same_Size_As_The_Band_Rises()
    {
        var lowBand = MilkyWayRenderModel.ProjectBand(
            latitudeDeg: 15,
            localSiderealDeg: 80,
            longitudeSteps: 8,
            latitudeSteps: 4);
        var otherBand = MilkyWayRenderModel.ProjectBand(
            latitudeDeg: 15,
            localSiderealDeg: 260,
            longitudeSteps: 8,
            latitudeSteps: 4);

        var first = MilkyWayBandMeshBuilder.Build(lowBand, radius: 40f);
        var second = MilkyWayBandMeshBuilder.Build(otherBand, radius: 40f);

        // Vintage Story sizes a mesh's buffers from the first upload, so a band whose index count
        // grew with the sky would have nowhere to put the extra triangles.
        Assert.Equal(lowBand.IndexCapacity, first.IndicesCount);
        Assert.Equal(second.IndicesCount, first.IndicesCount);
    }

    [Fact]
    public void The_Horizon_Fade_Rides_On_The_Vertex_Alpha()
    {
        var band = MilkyWayRenderModel.ProjectBand(latitudeDeg: 30, localSiderealDeg: 200);

        var mesh = MilkyWayBandMeshBuilder.Build(band, radius: 40f);

        var alphas = new List<int>();
        for (var index = 0; index < mesh.VerticesCount; index++)
        {
            alphas.Add(mesh.Rgba[(index * 4) + 3] & 0xFF);
        }

        Assert.Contains(0, alphas);
        Assert.Contains(255, alphas);
    }
}
