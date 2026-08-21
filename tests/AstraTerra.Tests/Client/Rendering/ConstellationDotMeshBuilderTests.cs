using AstraTerra.Client.Rendering;
using Vintagestory.API.MathTools;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The dots are the constellation lines, and there are more of them than there are stars. These pin
/// the geometry that lets all of them go into one mesh and one draw call.
/// </summary>
public sealed class ConstellationDotMeshBuilderTests
{
    private const float Radius = 40f;
    private const float DotSizeDeg = 0.14f;

    [Fact]
    public void Every_Dot_Becomes_One_Quad()
    {
        var mesh = ConstellationDotMeshBuilder.Build(Dots(3), Radius, DotSizeDeg, capacity: 3);

        Assert.Equal(12, mesh.VerticesCount);
        Assert.Equal(18, mesh.IndicesCount);
    }

    [Fact]
    public void A_Quad_Sits_On_The_Sky_Sphere_At_The_Right_Angular_Size()
    {
        var mesh = ConstellationDotMeshBuilder.Build(
            [Dot(0, 0, 1, 0)],
            Radius,
            angularSizeDeg: 2.0f,
            capacity: 1);

        var expectedHalfSize = Radius * MathF.Tan(2.0f * MathF.PI / 360f);
        for (var vertex = 0; vertex < 4; vertex++)
        {
            var offset = vertex * 3;
            var x = mesh.xyz[offset];
            var y = mesh.xyz[offset + 1];
            var z = mesh.xyz[offset + 2];

            // Corners sit a half-size away from the centre in each of the two axes across the line
            // of sight, so their distance from the sphere's centre is the diagonal.
            var distance = Math.Sqrt((x * x) + (y * y) + (z * z));
            Assert.Equal(Math.Sqrt((Radius * Radius) + (2 * expectedHalfSize * expectedHalfSize)), distance, 3);
            Assert.Equal(Radius, y, 3);
        }
    }

    /// <summary>
    /// Straight up and straight down are where a world-up reference axis degenerates, and a quad
    /// built from it would collapse to a point.
    /// </summary>
    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0.9995, 0.0316)]
    public void A_Dot_Anywhere_In_The_Sky_Gets_A_Quad_With_Area(double x, double y, double z)
    {
        var mesh = ConstellationDotMeshBuilder.Build([Dot(0, x, y, z)], Radius, angularSizeDeg: 2.0f, capacity: 1);

        Assert.Equal(4, mesh.VerticesCount);

        var expectedHalfSize = Radius * MathF.Tan(2.0f * MathF.PI / 360f);
        var width = Distance(mesh, 0, 1);
        var height = Distance(mesh, 1, 2);
        Assert.Equal(2 * expectedHalfSize, width, 3);
        Assert.Equal(2 * expectedHalfSize, height, 3);
    }

    private static double Distance(Vintagestory.API.Client.MeshData mesh, int firstVertex, int secondVertex)
    {
        var a = firstVertex * 3;
        var b = secondVertex * 3;
        var dx = mesh.xyz[a] - mesh.xyz[b];
        var dy = mesh.xyz[a + 1] - mesh.xyz[b + 1];
        var dz = mesh.xyz[a + 2] - mesh.xyz[b + 2];
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    [Fact]
    public void The_Tint_Rides_On_The_Vertices_Rather_Than_A_Per_Dot_Uniform()
    {
        var tint = new ConstellationTint(1f, 0f, 0f, 0.34f);
        var mesh = ConstellationDotMeshBuilder.Build(
            [new SkyConstellationDot(1, 100, 200, tint, 1, 0, 0)],
            Radius,
            DotSizeDeg,
            capacity: 1);

        var expected = ColorUtil.ColorFromRgba(255, 0, 0, 87);
        for (var vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(expected, BitConverter.ToInt32(
            [
                mesh.Rgba[(vertex * 4) + 0],
                mesh.Rgba[(vertex * 4) + 1],
                mesh.Rgba[(vertex * 4) + 2],
                mesh.Rgba[(vertex * 4) + 3]
            ]));
        }
    }

    /// <summary>
    /// Vintage Story sizes a mesh's buffers on first upload and never grows them, so the batch has to
    /// be built at a fixed capacity with the spare slots drawing nothing.
    /// </summary>
    [Fact]
    public void Spare_Capacity_Is_Filled_With_Invisible_Slots()
    {
        var mesh = ConstellationDotMeshBuilder.Build(Dots(1), Radius, DotSizeDeg, capacity: 4);

        Assert.Equal(16, mesh.VerticesCount);
        Assert.Equal(24, mesh.IndicesCount);
        for (var vertex = 4; vertex < mesh.VerticesCount; vertex++)
        {
            Assert.Equal(0, mesh.Rgba[(vertex * 4) + 3]);
        }
    }

    [Fact]
    public void More_Dots_Than_Capacity_Are_Dropped_Rather_Than_Overrunning_The_Buffer()
    {
        var mesh = ConstellationDotMeshBuilder.Build(Dots(10), Radius, DotSizeDeg, capacity: 4);

        Assert.Equal(16, mesh.VerticesCount);
        Assert.Equal(24, mesh.IndicesCount);
    }

    [Theory]
    [InlineData(0, 1024)]
    [InlineData(1, 1024)]
    [InlineData(1024, 1024)]
    [InlineData(1025, 2048)]
    [InlineData(3728, 4096)]
    public void Capacity_Grows_In_Steps_So_The_Mesh_Is_Not_Reallocated_Per_Edit(int dotCount, int expectedCapacity)
    {
        Assert.Equal(expectedCapacity, ConstellationDotMeshBuilder.GetCapacityFor(dotCount));
    }

    private static IReadOnlyList<SkyConstellationDot> Dots(int count)
    {
        var dots = new List<SkyConstellationDot>(count);
        for (var index = 0; index < count; index++)
        {
            var angle = index * 2.0 * Math.PI / Math.Max(1, count);
            dots.Add(Dot(index, Math.Cos(angle), 0.0, Math.Sin(angle)));
        }

        return dots;
    }

    private static SkyConstellationDot Dot(int id, double x, double y, double z)
        => new(id, 100 + id, 200 + id, new ConstellationTint(0.5f, 0.6f, 0.7f, 0.34f), x, y, z);
}
