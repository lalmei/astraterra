using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>One drawn constellation line: the two directions it runs between, and its colour.</summary>
public readonly record struct SkyConstellationLine(
    double StartX,
    double StartY,
    double StartZ,
    double EndX,
    double EndY,
    double EndZ,
    int Color);

/// <summary>
/// Bakes every constellation line into one mesh, as a ribbon per edge.
/// </summary>
/// <remarks>
/// These used to be dotted trails: a billboard every 0.65 deg along each edge, which came to some
/// 3700 sprites for the authored catalogue. Spacing fixed in degrees is spacing that magnifies, so
/// through a telescope the dots pulled apart into a row of specks, and holding their look at depth
/// would have meant tens of thousands of them.
/// <para>
/// A ribbon has no density to get wrong. One quad spans a whole edge whatever the magnification, so
/// the authored sky is 148 quads instead of thousands of sprites, and the only thing left to tune is
/// how wide the line draws.
/// </para>
/// <para>
/// The ribbon is widened along the normal of the plane through the two stars and the observer, which
/// is the one direction that reads as sideways from the centre of the sky sphere — so the line keeps
/// its width on screen without being rebuilt as the player turns. That the segment is drawn straight
/// rather than as a great-circle arc costs nothing either: a perspective camera at the centre of the
/// sphere projects both to the same line on screen.
/// </para>
/// </remarks>
public static class ConstellationLineMeshBuilder
{
    public const int VerticesPerLine = 4;
    public const int IndicesPerLine = 6;

    /// <param name="radius">Distance to the sky sphere the lines sit on.</param>
    /// <param name="widthDeg">How wide the ribbon draws, in degrees of sky.</param>
    /// <param name="endGapDeg">
    /// How far short of each star the line stops, so a pattern reads as stars joined by lines rather
    /// than as lines with stars buried under their ends.
    /// </param>
    public static MeshData Build(
        IReadOnlyList<SkyConstellationLine> lines,
        float radius,
        float widthDeg,
        float endGapDeg,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthDeg);
        ArgumentOutOfRangeException.ThrowIfNegative(endGapDeg);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var meshData = new MeshData(
            Math.Max(1, capacity * VerticesPerLine),
            Math.Max(1, capacity * IndicesPerLine),
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        var drawn = Math.Min(lines.Count, capacity);
        var slotsUsed = 0;
        for (var index = 0; index < drawn; index++)
        {
            if (Add(meshData, lines[index], radius, widthDeg, endGapDeg))
            {
                slotsUsed++;
            }
        }

        for (var slot = slotsUsed; slot < capacity; slot++)
        {
            AddUnusedSlot(meshData, radius);
        }

        return meshData;
    }

    /// <summary>Lines needed for <paramref name="count"/>, rounded up so the mesh is not resized for every edit.</summary>
    public static int GetCapacityFor(int count, int growthStep = 256)
        => SkyBillboardMeshBuilder.GetCapacityFor(count, growthStep);

    /// <summary>Adds one ribbon, or reports that the edge was too short to draw one.</summary>
    private static bool Add(
        MeshData meshData,
        SkyConstellationLine line,
        float radius,
        float widthDeg,
        float endGapDeg)
    {
        var (startX, startY, startZ) = Normalize(line.StartX, line.StartY, line.StartZ);
        var (endX, endY, endZ) = Normalize(line.EndX, line.EndY, line.EndZ);

        // The plane through both stars and the observer. Its normal is the only offset that moves a
        // point sideways rather than along the line, seen from the centre of the sphere.
        var (sideX, sideY, sideZ) = Cross(startX, startY, startZ, endX, endY, endZ);
        var sideLength = Math.Sqrt((sideX * sideX) + (sideY * sideY) + (sideZ * sideZ));
        if (sideLength < 1e-9)
        {
            // Either end of a zero-length edge, or two stars on exactly opposite sides of the sky:
            // no plane, and nothing worth drawing.
            return false;
        }

        sideX /= sideLength;
        sideY /= sideLength;
        sideZ /= sideLength;

        var lengthRad = Math.Acos(Math.Clamp(Dot(startX, startY, startZ, endX, endY, endZ), -1.0, 1.0));
        var lengthDeg = lengthRad * 180.0 / Math.PI;

        // Capped at a little under half, so the two insets cannot cross and turn the ribbon inside
        // out on an edge shorter than twice the gap.
        var gap = lengthDeg <= 0 ? 0.0 : Math.Min(endGapDeg / lengthDeg, 0.45);
        var (fromX, fromY, fromZ) = Slerp(startX, startY, startZ, endX, endY, endZ, gap, lengthRad);
        var (toX, toY, toZ) = Slerp(startX, startY, startZ, endX, endY, endZ, 1.0 - gap, lengthRad);

        var halfWidth = Math.Tan(widthDeg * Math.PI / 360.0);
        var firstVertex = meshData.VerticesCount;

        AddCorner(meshData, fromX, fromY, fromZ, sideX, sideY, sideZ, -halfWidth, radius, 0f, 0f, line.Color);
        AddCorner(meshData, fromX, fromY, fromZ, sideX, sideY, sideZ, halfWidth, radius, 0f, 1f, line.Color);
        AddCorner(meshData, toX, toY, toZ, sideX, sideY, sideZ, halfWidth, radius, 1f, 1f, line.Color);
        AddCorner(meshData, toX, toY, toZ, sideX, sideY, sideZ, -halfWidth, radius, 1f, 0f, line.Color);

        AddQuadIndices(meshData, firstVertex);
        return true;
    }

    /// <summary>
    /// A slot the sky is not using, collapsed to a transparent point. The mesh is always built at
    /// full capacity because Vintage Story sizes a mesh's buffers from its first upload.
    /// </summary>
    private static void AddUnusedSlot(MeshData meshData, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        for (var corner = 0; corner < VerticesPerLine; corner++)
        {
            meshData.AddVertexWithFlags(0f, radius, 0f, 0f, 0f, ColorUtil.ColorFromRgba(0, 0, 0, 0), 0);
        }

        AddQuadIndices(meshData, firstVertex);
    }

    private static void AddCorner(
        MeshData meshData,
        double directionX,
        double directionY,
        double directionZ,
        double sideX,
        double sideY,
        double sideZ,
        double halfWidth,
        float radius,
        float u,
        float v,
        int color)
    {
        var (x, y, z) = Normalize(
            directionX + (sideX * halfWidth),
            directionY + (sideY * halfWidth),
            directionZ + (sideZ * halfWidth));

        meshData.AddVertexWithFlags((float)x * radius, (float)y * radius, (float)z * radius, u, v, color, 0);
    }

    private static void AddQuadIndices(MeshData meshData, int firstVertex)
    {
        meshData.AddIndex(firstVertex);
        meshData.AddIndex(firstVertex + 1);
        meshData.AddIndex(firstVertex + 2);
        meshData.AddIndex(firstVertex);
        meshData.AddIndex(firstVertex + 2);
        meshData.AddIndex(firstVertex + 3);
    }

    private static (double X, double Y, double Z) Normalize(double x, double y, double z)
    {
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return length < 1e-12 ? (0.0, 1.0, 0.0) : (x / length, y / length, z / length);
    }

    private static (double X, double Y, double Z) Cross(
        double leftX,
        double leftY,
        double leftZ,
        double rightX,
        double rightY,
        double rightZ)
        => ((leftY * rightZ) - (leftZ * rightY),
            (leftZ * rightX) - (leftX * rightZ),
            (leftX * rightY) - (leftY * rightX));

    private static double Dot(double leftX, double leftY, double leftZ, double rightX, double rightY, double rightZ)
        => (leftX * rightX) + (leftY * rightY) + (leftZ * rightZ);

    /// <summary>
    /// Walks a fraction of the way along the arc between two directions, rather than along the chord
    /// under it. The difference is what makes an end gap the angle it was asked for: a thirtieth of
    /// the way along the chord of a 90 deg edge is only two thirds of that angle from the star.
    /// </summary>
    private static (double X, double Y, double Z) Slerp(
        double startX,
        double startY,
        double startZ,
        double endX,
        double endY,
        double endZ,
        double t,
        double lengthRad)
    {
        var sinLength = Math.Sin(lengthRad);
        if (sinLength < 1e-9)
        {
            // Coincident or opposite: no arc to walk, and the caller has already rejected the pair.
            return (startX, startY, startZ);
        }

        var fromWeight = Math.Sin((1.0 - t) * lengthRad) / sinLength;
        var toWeight = Math.Sin(t * lengthRad) / sinLength;

        return Normalize(
            (startX * fromWeight) + (endX * toWeight),
            (startY * fromWeight) + (endY * toWeight),
            (startZ * fromWeight) + (endZ * toWeight));
    }
}
