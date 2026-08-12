using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Batches the active streaks into the single mesh the sky pass draws.
/// </summary>
/// <remarks>
/// The mesh is always built at full capacity, however few streaks are burning. Vintage Story sizes
/// a mesh's GPU buffers from the vertex count of the first <c>UploadMesh</c> and never grows them —
/// <c>UpdateMesh</c> only writes into the space already allocated, while still advertising the new
/// index count to the draw call. A mesh first uploaded on a one-meteor frame would therefore drop
/// every later frame's vertices and then draw past the end of its own index buffer as soon as two
/// meteors overlapped.
/// </remarks>
public static class MeteorStreakMeshBuilder
{
    private readonly record struct TrailSection(double Position, double WidthScale, double AlphaScale);

    private static readonly TrailSection[] Sections =
    [
        new(-0.50, 0.05, 0.00),
        new(-0.28, 0.65, 0.20),
        new(0.34, 1.00, 1.00),
        new(0.50, 0.08, 0.00)
    ];

    private static readonly int VerticesPerStreak = Sections.Length * 2;
    private static readonly int IndicesPerStreak = (Sections.Length - 1) * 6;

    public static MeshData Build(
        IReadOnlyList<RenderedMeteorStreak> streaks,
        float radius,
        int capacity = MeteorShowerVisualModel.MaximumActiveStreaks)
    {
        ArgumentNullException.ThrowIfNull(streaks);
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        var meshData = new MeshData(
            capacity * VerticesPerStreak,
            capacity * IndicesPerStreak,
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        var drawnStreaks = 0;
        foreach (var streak in streaks.Take(capacity))
        {
            AddStreak(meshData, streak, radius);
            drawnStreaks++;
        }

        for (var slot = drawnStreaks; slot < capacity; slot++)
        {
            AddUnusedStreakSlot(meshData, radius);
        }

        return meshData;
    }

    private static void AddStreak(MeshData meshData, RenderedMeteorStreak streak, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        var halfWidthTangent = Math.Tan(streak.AngularWidthDeg * Math.PI / 360.0);

        for (var sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
        {
            var section = Sections[sectionIndex];
            var alongRadians = streak.AngularLengthDeg * section.Position * Math.PI / 180.0;
            var center = Normalize(Add(
                Scale(streak.Center, Math.Cos(alongRadians)),
                Scale(streak.Tangent, Math.Sin(alongRadians))));
            var tangent = Normalize(Add(
                Scale(streak.Center, -Math.Sin(alongRadians)),
                Scale(streak.Tangent, Math.Cos(alongRadians))));
            var side = Normalize(Cross(center, tangent));
            var sideAmount = halfWidthTangent * section.WidthScale;
            var left = Normalize(Add(center, Scale(side, -sideAmount)));
            var right = Normalize(Add(center, Scale(side, sideAmount)));
            var alpha = (int)Math.Round(Math.Clamp(streak.Alpha * section.AlphaScale, 0.0, 1.0) * 255.0);
            var color = ColorUtil.ColorFromRgba(220, 235, 255, alpha);
            var u = sectionIndex / (float)(Sections.Length - 1);

            meshData.AddVertexWithFlags(
                (float)left.X * radius,
                (float)left.Y * radius,
                (float)left.Z * radius,
                u,
                0f,
                color,
                0);
            meshData.AddVertexWithFlags(
                (float)right.X * radius,
                (float)right.Y * radius,
                (float)right.Z * radius,
                u,
                1f,
                color,
                0);
        }

        AddStreakIndices(meshData, firstVertex);
    }

    /// <summary>
    /// Fills a streak's worth of the batch with nothing: eight coincident, fully transparent
    /// vertices whose triangles all have zero area and produce no fragments. Keeping the slot
    /// costs a few degenerate triangles and buys a mesh whose size never changes between frames.
    /// </summary>
    private static void AddUnusedStreakSlot(MeshData meshData, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        var color = ColorUtil.ColorFromRgba(0, 0, 0, 0);

        for (var vertexIndex = 0; vertexIndex < VerticesPerStreak; vertexIndex++)
        {
            meshData.AddVertexWithFlags(0f, radius, 0f, 0f, 0f, color, 0);
        }

        AddStreakIndices(meshData, firstVertex);
    }

    private static void AddStreakIndices(MeshData meshData, int firstVertex)
    {
        for (var sectionIndex = 0; sectionIndex < Sections.Length - 1; sectionIndex++)
        {
            var left = firstVertex + (sectionIndex * 2);
            var right = left + 1;
            var nextLeft = left + 2;
            var nextRight = left + 3;
            meshData.AddIndex(left);
            meshData.AddIndex(right);
            meshData.AddIndex(nextRight);
            meshData.AddIndex(left);
            meshData.AddIndex(nextRight);
            meshData.AddIndex(nextLeft);
        }
    }

    private static MeteorSkyDirection Add(MeteorSkyDirection first, MeteorSkyDirection second)
        => new(first.X + second.X, first.Y + second.Y, first.Z + second.Z);

    private static MeteorSkyDirection Scale(MeteorSkyDirection value, double factor)
        => new(value.X * factor, value.Y * factor, value.Z * factor);

    private static MeteorSkyDirection Cross(MeteorSkyDirection first, MeteorSkyDirection second)
        => new(
            (first.Y * second.Z) - (first.Z * second.Y),
            (first.Z * second.X) - (first.X * second.Z),
            (first.X * second.Y) - (first.Y * second.X));

    private static MeteorSkyDirection Normalize(MeteorSkyDirection direction)
    {
        var length = Math.Sqrt(
            (direction.X * direction.X) +
            (direction.Y * direction.Y) +
            (direction.Z * direction.Z));
        if (length <= double.Epsilon)
        {
            throw new ArgumentException("Meteor streak directions must be non-zero.");
        }

        return Scale(direction, 1.0 / length);
    }
}
