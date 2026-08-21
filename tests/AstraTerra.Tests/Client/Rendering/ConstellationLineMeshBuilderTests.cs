using AstraTerra.Client.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The lines are the constellations. These pin the ribbon geometry that replaced a dotted trail of
/// thousands of sprites, and that has to hold its width against a magnified sky.
/// </summary>
public sealed class ConstellationLineMeshBuilderTests
{
    private const float Radius = 40f;
    private const float WidthDeg = 0.10f;

    [Fact]
    public void Every_Edge_Becomes_One_Quad()
    {
        var mesh = ConstellationLineMeshBuilder.Build(Lines(3), Radius, WidthDeg, endGapDeg: 0f, capacity: 3);

        Assert.Equal(12, mesh.VerticesCount);
        Assert.Equal(18, mesh.IndicesCount);
    }

    [Fact]
    public void A_Ribbon_Sits_On_The_Sky_Sphere()
    {
        var mesh = ConstellationLineMeshBuilder.Build(
            [Line(1, 0, 0, 0, 1, 0)],
            Radius,
            widthDeg: 2.0f,
            endGapDeg: 0f,
            capacity: 1);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            var offset = vertex * 3;
            var distance = Math.Sqrt(
                (mesh.xyz[offset] * mesh.xyz[offset]) +
                (mesh.xyz[offset + 1] * mesh.xyz[offset + 1]) +
                (mesh.xyz[offset + 2] * mesh.xyz[offset + 2]));

            Assert.Equal(Radius, distance, 3);
        }
    }

    /// <summary>
    /// Width is what the eye reads as the weight of the line, so it has to come out as asked rather
    /// than varying with where in the sky the edge happens to sit.
    /// </summary>
    [Theory]
    [InlineData(1, 0, 0, 0, 1, 0)]
    [InlineData(0, 1, 0, 0, 0.9995, 0.0316)]
    [InlineData(0.3, -0.5, 0.81, -0.7, 0.1, 0.7)]
    public void A_Ribbon_Is_As_Wide_As_It_Was_Asked_To_Be(
        double startX,
        double startY,
        double startZ,
        double endX,
        double endY,
        double endZ)
    {
        const float widthDeg = 2.0f;
        var mesh = ConstellationLineMeshBuilder.Build(
            [Line(startX, startY, startZ, endX, endY, endZ)],
            Radius,
            widthDeg,
            endGapDeg: 0f,
            capacity: 1);

        // Corners 0 and 1 are the two sides of the same end, so the angle between them is the width.
        Assert.Equal(widthDeg, AngleBetweenDeg(mesh, 0, 1), 3);
        Assert.Equal(widthDeg, AngleBetweenDeg(mesh, 2, 3), 3);
    }

    /// <summary>
    /// The ribbon widens across the line, not along it: the two corners of one end have to stay the
    /// same distance from the star they belong to.
    /// </summary>
    [Fact]
    public void A_Ribbon_Widens_Across_The_Line_Rather_Than_Along_It()
    {
        var mesh = ConstellationLineMeshBuilder.Build(
            [Line(1, 0, 0, 0, 1, 0)],
            Radius,
            widthDeg: 2.0f,
            endGapDeg: 0f,
            capacity: 1);

        Assert.Equal(AngleFromDeg(mesh, 0, 1, 0, 0), AngleFromDeg(mesh, 1, 1, 0, 0), 3);
        Assert.Equal(AngleFromDeg(mesh, 2, 0, 1, 0), AngleFromDeg(mesh, 3, 0, 1, 0), 3);
    }

    [Fact]
    public void A_Line_Stops_Short_Of_The_Stars_It_Joins()
    {
        const float gapDeg = 3.0f;
        var mesh = ConstellationLineMeshBuilder.Build(
            [Line(1, 0, 0, 0, 1, 0)],
            Radius,
            WidthDeg,
            gapDeg,
            capacity: 1);

        // The 90 deg edge runs from +X to +Y, so each end should have backed off by the gap.
        Assert.Equal(gapDeg, AngleFromDeg(mesh, 0, 1, 0, 0), 1);
        Assert.Equal(gapDeg, AngleFromDeg(mesh, 3, 0, 1, 0), 1);
    }

    /// <summary>A gap wider than the edge would turn the line inside out.</summary>
    [Fact]
    public void A_Gap_Wider_Than_The_Edge_Still_Leaves_A_Line()
    {
        var mesh = ConstellationLineMeshBuilder.Build(
            [Line(1, 0, 0, 1, 0.02, 0)],
            Radius,
            WidthDeg,
            endGapDeg: 30f,
            capacity: 1);

        Assert.Equal(4, mesh.VerticesCount);
        Assert.True(Distance(mesh, 0, 3) > 0, "The line collapsed to nothing.");
    }

    [Fact]
    public void An_Edge_With_No_Length_Draws_Nothing_But_Keeps_The_Mesh_At_Capacity()
    {
        var mesh = ConstellationLineMeshBuilder.Build(
            [Line(1, 0, 0, 1, 0, 0)],
            Radius,
            WidthDeg,
            endGapDeg: 0f,
            capacity: 2);

        Assert.Equal(8, mesh.VerticesCount);
        Assert.Equal(12, mesh.IndicesCount);
        for (var vertex = 0; vertex < 8; vertex++)
        {
            Assert.Equal(0, mesh.Rgba[(vertex * 4) + 3]);
        }
    }

    [Fact]
    public void A_Lines_Colour_Rides_On_Its_Vertices()
    {
        var color = ColorUtil.ColorFromRgba(12, 34, 56, 78);
        var mesh = ConstellationLineMeshBuilder.Build(
            [new SkyConstellationLine(1, 0, 0, 0, 1, 0, color)],
            Radius,
            WidthDeg,
            endGapDeg: 0f,
            capacity: 1);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(12, mesh.Rgba[vertex * 4] & 0xff);
            Assert.Equal(34, mesh.Rgba[(vertex * 4) + 1] & 0xff);
            Assert.Equal(56, mesh.Rgba[(vertex * 4) + 2] & 0xff);
            Assert.Equal(78, mesh.Rgba[(vertex * 4) + 3] & 0xff);
        }
    }

    [Fact]
    public void Unused_Capacity_Is_Invisible_Rather_Than_Absent()
    {
        var mesh = ConstellationLineMeshBuilder.Build(Lines(1), Radius, WidthDeg, endGapDeg: 0f, capacity: 4);

        Assert.Equal(16, mesh.VerticesCount);
        for (var vertex = 4; vertex < 16; vertex++)
        {
            Assert.Equal(0, mesh.Rgba[(vertex * 4) + 3]);
        }
    }

    [Fact]
    public void More_Lines_Than_Capacity_Are_Dropped_Rather_Than_Overflowing()
    {
        var mesh = ConstellationLineMeshBuilder.Build(Lines(10), Radius, WidthDeg, endGapDeg: 0f, capacity: 4);

        Assert.Equal(16, mesh.VerticesCount);
    }

    [Theory]
    [InlineData(0, 256)]
    [InlineData(1, 256)]
    [InlineData(256, 256)]
    [InlineData(257, 512)]
    public void GetCapacityFor_Rounds_Up_To_A_Growth_Step(int lineCount, int expectedCapacity)
    {
        Assert.Equal(expectedCapacity, ConstellationLineMeshBuilder.GetCapacityFor(lineCount));
    }

    private static IReadOnlyList<SkyConstellationLine> Lines(int count)
    {
        var lines = new List<SkyConstellationLine>();
        for (var index = 0; index < count; index++)
        {
            var offset = 0.1 * (index + 1);
            lines.Add(Line(1, 0, 0, Math.Cos(offset), Math.Sin(offset), 0));
        }

        return lines;
    }

    private static SkyConstellationLine Line(
        double startX,
        double startY,
        double startZ,
        double endX,
        double endY,
        double endZ)
        => new(startX, startY, startZ, endX, endY, endZ, ColorUtil.ColorFromRgba(255, 255, 255, 128));

    private static double AngleBetweenDeg(MeshData mesh, int firstVertex, int secondVertex)
    {
        var (ax, ay, az) = Vertex(mesh, firstVertex);
        var (bx, by, bz) = Vertex(mesh, secondVertex);
        return AngleDeg(ax, ay, az, bx, by, bz);
    }

    private static double AngleFromDeg(MeshData mesh, int vertex, double x, double y, double z)
    {
        var (vx, vy, vz) = Vertex(mesh, vertex);
        return AngleDeg(vx, vy, vz, x, y, z);
    }

    private static double AngleDeg(double ax, double ay, double az, double bx, double by, double bz)
    {
        var aLength = Math.Sqrt((ax * ax) + (ay * ay) + (az * az));
        var bLength = Math.Sqrt((bx * bx) + (by * by) + (bz * bz));
        var dot = ((ax * bx) + (ay * by) + (az * bz)) / (aLength * bLength);
        return Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
    }

    private static double Distance(MeshData mesh, int firstVertex, int secondVertex)
    {
        var (ax, ay, az) = Vertex(mesh, firstVertex);
        var (bx, by, bz) = Vertex(mesh, secondVertex);
        return Math.Sqrt(((ax - bx) * (ax - bx)) + ((ay - by) * (ay - by)) + ((az - bz) * (az - bz)));
    }

    private static (double X, double Y, double Z) Vertex(MeshData mesh, int vertex)
        => (mesh.xyz[vertex * 3], mesh.xyz[(vertex * 3) + 1], mesh.xyz[(vertex * 3) + 2]);
}
