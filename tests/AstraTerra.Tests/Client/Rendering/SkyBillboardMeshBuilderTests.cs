using AstraTerra.Client.Rendering;
using Vintagestory.API.MathTools;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The geometry behind both batches: constellation dots and the star field. Around three thousand
/// stars used to be three thousand draw calls; these pin what replaced them.
/// </summary>
public sealed class SkyBillboardMeshBuilderTests
{
    private const float Radius = 40f;

    [Fact]
    public void Every_Sprite_Becomes_One_Quad()
    {
        var mesh = SkyBillboardMeshBuilder.Build(Billboards(5, 0.1f), Radius, capacity: 5);

        Assert.Equal(20, mesh.VerticesCount);
        Assert.Equal(30, mesh.IndicesCount);
    }

    /// <summary>
    /// Stars vary in size across the batch, which a single per-draw uniform could never express.
    /// </summary>
    [Fact]
    public void Each_Sprite_Keeps_Its_Own_Angular_Size()
    {
        var small = new SkyBillboard(1, 0, 0, 0.5f, ColorUtil.ColorFromRgba(255, 255, 255, 255));
        var large = new SkyBillboard(0, 0, 1, 2.0f, ColorUtil.ColorFromRgba(255, 255, 255, 255));

        var mesh = SkyBillboardMeshBuilder.Build([small, large], Radius, capacity: 2);

        Assert.Equal(2 * Radius * MathF.Tan(0.5f * MathF.PI / 360f), Width(mesh, 0), 3);
        Assert.Equal(2 * Radius * MathF.Tan(2.0f * MathF.PI / 360f), Width(mesh, 1), 3);
    }

    /// <summary>
    /// Colour rides on the vertices, which is what allows one draw call per sprite rather than one
    /// per star.
    /// </summary>
    [Fact]
    public void Each_Sprite_Keeps_Its_Own_Colour()
    {
        var red = ColorUtil.ColorFromRgba(255, 0, 0, 200);
        var blue = ColorUtil.ColorFromRgba(0, 0, 255, 90);

        var mesh = SkyBillboardMeshBuilder.Build(
            [
                new SkyBillboard(1, 0, 0, 0.5f, red),
                new SkyBillboard(0, 0, 1, 0.5f, blue)
            ],
            Radius,
            capacity: 2);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(200, mesh.Rgba[(vertex * 4) + 3]);
            Assert.Equal(90, mesh.Rgba[((vertex + 4) * 4) + 3]);
        }
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, -1, 0)]
    public void A_Sprite_Anywhere_In_The_Sky_Gets_A_Quad_With_Area(double x, double y, double z)
    {
        var mesh = SkyBillboardMeshBuilder.Build(
            [new SkyBillboard(x, y, z, 2.0f, ColorUtil.ColorFromRgba(255, 255, 255, 255))],
            Radius,
            capacity: 1);

        Assert.Equal(2 * Radius * MathF.Tan(2.0f * MathF.PI / 360f), Width(mesh, 0), 3);
    }

    [Fact]
    public void Spare_Capacity_Is_Filled_With_Invisible_Slots()
    {
        var mesh = SkyBillboardMeshBuilder.Build(Billboards(1, 0.5f), Radius, capacity: 4);

        Assert.Equal(16, mesh.VerticesCount);
        for (var vertex = 4; vertex < mesh.VerticesCount; vertex++)
        {
            Assert.Equal(0, mesh.Rgba[(vertex * 4) + 3]);
        }
    }

    [Fact]
    public void More_Sprites_Than_Capacity_Are_Dropped_Rather_Than_Overrunning_The_Buffer()
    {
        var mesh = SkyBillboardMeshBuilder.Build(Billboards(50, 0.5f), Radius, capacity: 8);

        Assert.Equal(32, mesh.VerticesCount);
        Assert.Equal(48, mesh.IndicesCount);
    }

    private static double Width(Vintagestory.API.Client.MeshData mesh, int quadIndex)
    {
        var a = quadIndex * 4 * 3;
        var b = a + 3;
        var dx = mesh.xyz[a] - mesh.xyz[b];
        var dy = mesh.xyz[a + 1] - mesh.xyz[b + 1];
        var dz = mesh.xyz[a + 2] - mesh.xyz[b + 2];
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static IReadOnlyList<SkyBillboard> Billboards(int count, float angularSizeDeg)
    {
        var billboards = new List<SkyBillboard>(count);
        for (var index = 0; index < count; index++)
        {
            var angle = index * 2.0 * Math.PI / Math.Max(1, count);
            billboards.Add(new SkyBillboard(
                Math.Cos(angle),
                0,
                Math.Sin(angle),
                angularSizeDeg,
                ColorUtil.ColorFromRgba(255, 255, 255, 255)));
        }

        return billboards;
    }
}
