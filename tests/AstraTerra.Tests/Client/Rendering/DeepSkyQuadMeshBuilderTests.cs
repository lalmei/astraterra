using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class DeepSkyQuadMeshBuilderTests
{
    [Fact]
    public void Build_Tessellates_All_Vertices_On_The_Sky_Sphere()
    {
        DeepSkyDirection[] corners =
        [
            Normalize(-0.1, -0.1, -1),
            Normalize(0.1, -0.1, -1),
            Normalize(0.1, 0.1, -1),
            Normalize(-0.1, 0.1, -1)
        ];

        var mesh = DeepSkyQuadMeshBuilder.Build(corners, radius: 40f, subdivisions: 2);

        Assert.Equal(9, mesh.VerticesCount);
        Assert.Equal(24, mesh.IndicesCount);
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
    public void Build_Preserves_Registered_Corners_And_Converts_Texture_Row_Origin()
    {
        DeepSkyDirection[] corners =
        [
            Normalize(-0.2, -0.1, -1),
            Normalize(0.1, -0.2, -1),
            Normalize(0.2, 0.1, -1),
            Normalize(-0.1, 0.2, -1)
        ];

        var mesh = DeepSkyQuadMeshBuilder.Build(corners, radius: 10f, subdivisions: 2);

        AssertVertex(mesh.xyz, 0, corners[0], 10f);
        AssertVertex(mesh.xyz, 2, corners[1], 10f);
        AssertVertex(mesh.xyz, 8, corners[2], 10f);
        AssertVertex(mesh.xyz, 6, corners[3], 10f);
        Assert.Equal([0f, 1f], mesh.Uv.Take(2));
        Assert.Equal([1f, 1f], mesh.Uv.Skip(4).Take(2));
        Assert.Equal([1f, 0f], mesh.Uv.Skip(16).Take(2));
        Assert.Equal([0f, 0f], mesh.Uv.Skip(12).Take(2));
    }

    private static DeepSkyDirection Normalize(double x, double y, double z)
    {
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return new DeepSkyDirection(x / length, y / length, z / length);
    }

    private static void AssertVertex(float[] positions, int vertexIndex, DeepSkyDirection expected, float radius)
    {
        var offset = vertexIndex * 3;
        Assert.Equal(expected.X * radius, positions[offset], precision: 5);
        Assert.Equal(expected.Y * radius, positions[offset + 1], precision: 5);
        Assert.Equal(expected.Z * radius, positions[offset + 2], precision: 5);
    }
}
