using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class CometTailMeshBuilderTests
{
    private const float Radius = 39.9f;

    private static RenderedComet Comet(double tailLengthDeg = 20.0, double prominence = 1.0)
        => new(
            "test",
            "Test",
            new RenderedBody(
                new EquatorialCoordinates(0, 0),
                VisualMagnitude: -1.0,
                AzimuthDeg: 0,
                AltitudeDeg: 90,
                Brightness: 1.0,
                DirectionX: 0,
                DirectionY: 1,
                DirectionZ: 0,
                Size: 12.0),
            prominence,
            tailLengthDeg,
            new SkyDirection(1, 0, 0),
            1f,
            1f,
            1f);

    /// <summary>
    /// Vintage Story sizes a mesh's GPU buffers from the first upload and never grows them, so every
    /// build has to be the same size however many comets are up — otherwise the first quiet frame
    /// permanently caps the buffer below what an apparition needs.
    /// </summary>
    [Fact]
    public void Every_Build_Is_The_Same_Size_Whatever_Is_Drawing()
    {
        var empty = CometTailMeshBuilder.Build([], Radius);
        var one = CometTailMeshBuilder.Build([Comet()], Radius);
        var many = CometTailMeshBuilder.Build([Comet(), Comet(), Comet()], Radius);

        Assert.Equal(empty.VerticesCount, one.VerticesCount);
        Assert.Equal(empty.VerticesCount, many.VerticesCount);
        Assert.Equal(empty.IndicesCount, one.IndicesCount);
        Assert.Equal(empty.IndicesCount, many.IndicesCount);
    }

    [Fact]
    public void More_Comets_Than_The_Batch_Holds_Does_Not_Overflow_It()
    {
        var capacity = CometTailMeshBuilder.Build([], Radius, capacity: 2);
        var overflowing = CometTailMeshBuilder.Build([Comet(), Comet(), Comet(), Comet()], Radius, capacity: 2);

        Assert.Equal(capacity.VerticesCount, overflowing.VerticesCount);
        Assert.Equal(capacity.IndicesCount, overflowing.IndicesCount);
    }

    [Fact]
    public void Every_Vertex_Sits_On_The_Sky_Sphere()
    {
        var mesh = CometTailMeshBuilder.Build([Comet(tailLengthDeg: 40.0)], Radius);

        for (var index = 0; index < mesh.VerticesCount; index++)
        {
            var x = mesh.xyz[index * 3];
            var y = mesh.xyz[(index * 3) + 1];
            var z = mesh.xyz[(index * 3) + 2];
            Assert.Equal(Radius, Math.Sqrt((x * x) + (y * y) + (z * z)), 3);
        }
    }

    /// <summary>
    /// A comet weeks from perihelion has a coma and no tail. Drawing a ribbon a fraction of a degree
    /// long would be a bright dash beside it, which is the opposite of building up gradually.
    /// </summary>
    [Fact]
    public void A_Comet_With_No_Tail_Yet_Draws_Nothing()
    {
        var mesh = CometTailMeshBuilder.Build([Comet(tailLengthDeg: 0.0, prominence: 0.0)], Radius);

        Assert.All(mesh.Rgba.Take(CometTailMeshBuilder.MaximumDrawnTails * 8 * 4), channel => Assert.Equal(0, channel));
    }

    [Fact]
    public void The_Tail_Fades_Out_Along_Its_Length()
    {
        var mesh = CometTailMeshBuilder.Build([Comet()], Radius);

        // Alpha is the fourth channel of each vertex, and the first section sits at the coma.
        var atComa = mesh.Rgba[3];
        var atTip = mesh.Rgba[(6 * 4) + 3];

        Assert.NotEqual(0, atComa);
        Assert.Equal(0, atTip);
    }

    [Fact]
    public void A_Zero_Radius_Sky_Is_Rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CometTailMeshBuilder.Build([Comet()], 0f));
    }
}
