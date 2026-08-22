using AstraTerra.Astronomy;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Batches every visible comet's tail into the one mesh the sky pass draws.
/// </summary>
/// <remarks>
/// A tail is a ribbon swept along the great circle that runs anti-sunward from the coma — the same
/// construction <see cref="MeteorStreakMeshBuilder"/> uses for a streak, and for the same reason: a
/// straight quad hung in front of the sky would visibly leave the sphere over the twenty-odd degrees
/// a bright comet spans.
/// <para>
/// Like the streak mesh, this is always built at full capacity. Vintage Story sizes a mesh's GPU
/// buffers from the first upload and never grows them, so a mesh first built on a no-comet frame
/// would have nowhere to put the vertices of the apparition it was made for.
/// </para>
/// </remarks>
public static class CometTailMeshBuilder
{
    /// <summary>Tails drawn at once. Well past the number of comets that can be up together.</summary>
    public const int MaximumDrawnTails = 8;

    /// <summary>
    /// Half-width at the far end, as a fraction of the tail's length. A real dust tail spreads to
    /// something like a sixth of its length by the time it fades out.
    /// </summary>
    public const double TailWidthRatio = 0.16;

    /// <param name="Position">Fraction of the way along the tail, 0 at the coma.</param>
    private readonly record struct TailSection(double Position, double WidthScale, double AlphaScale);

    /// <summary>
    /// A tail is brightest and narrowest where it leaves the coma and spreads as it fades, so the
    /// widening and the fading run in opposite directions along the same ribbon.
    /// </summary>
    private static readonly TailSection[] Sections =
    [
        new(0.00, 0.10, 1.00),
        new(0.18, 0.45, 0.72),
        new(0.50, 0.80, 0.33),
        new(1.00, 1.00, 0.00)
    ];

    private static readonly int VerticesPerTail = Sections.Length * 2;
    private static readonly int IndicesPerTail = (Sections.Length - 1) * 6;

    public static MeshData Build(
        IReadOnlyList<RenderedComet> comets,
        float radius,
        int capacity = MaximumDrawnTails)
    {
        ArgumentNullException.ThrowIfNull(comets);
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        var meshData = new MeshData(
            capacity * VerticesPerTail,
            capacity * IndicesPerTail,
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        var drawnTails = 0;
        foreach (var comet in comets.Take(capacity))
        {
            // A comet still weeks out has a coma and no tail worth drawing; its slot is spent as an
            // empty one rather than on a ribbon a fraction of a degree long.
            if (comet.TailLengthDeg > 0.05 && comet.Prominence > 0.0)
            {
                AddTail(meshData, comet, radius);
            }
            else
            {
                AddUnusedTailSlot(meshData, radius);
            }

            drawnTails++;
        }

        for (var slot = drawnTails; slot < capacity; slot++)
        {
            AddUnusedTailSlot(meshData, radius);
        }

        return meshData;
    }

    private static void AddTail(MeshData meshData, RenderedComet comet, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        var coma = Normalize(comet.ComaDirection);
        var axis = Normalize(comet.TailAxis);
        var halfWidthTangent = Math.Tan(comet.TailLengthDeg * TailWidthRatio * Math.PI / 360.0);

        var red = (int)Math.Round(Math.Clamp(comet.TintR, 0f, 1f) * 255.0);
        var green = (int)Math.Round(Math.Clamp(comet.TintG, 0f, 1f) * 255.0);
        var blue = (int)Math.Round(Math.Clamp(comet.TintB, 0f, 1f) * 255.0);

        // The tail never draws more solidly than the apparition warrants, so it builds and fades with
        // the coma instead of snapping to full strength the moment the comet clears the horizon.
        var tailAlpha = Math.Clamp(comet.Prominence * comet.Brightness, 0.0, 1.0);

        for (var sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
        {
            var section = Sections[sectionIndex];
            var alongRadians = comet.TailLengthDeg * section.Position * Math.PI / 180.0;
            var center = Normalize(Add(
                Scale(coma, Math.Cos(alongRadians)),
                Scale(axis, Math.Sin(alongRadians))));
            var tangent = Normalize(Add(
                Scale(coma, -Math.Sin(alongRadians)),
                Scale(axis, Math.Cos(alongRadians))));
            var side = Normalize(Cross(center, tangent));
            var sideAmount = halfWidthTangent * section.WidthScale;
            var left = Normalize(Add(center, Scale(side, -sideAmount)));
            var right = Normalize(Add(center, Scale(side, sideAmount)));
            var alpha = (int)Math.Round(Math.Clamp(tailAlpha * section.AlphaScale, 0.0, 1.0) * 255.0);
            var color = ColorUtil.ColorFromRgba(red, green, blue, alpha);
            var u = (float)section.Position;

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

        AddTailIndices(meshData, firstVertex);
    }

    /// <summary>
    /// A tail's worth of nothing: coincident, fully transparent vertices whose triangles have zero
    /// area. Costs a few degenerate triangles and buys a mesh whose size never changes.
    /// </summary>
    private static void AddUnusedTailSlot(MeshData meshData, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        var color = ColorUtil.ColorFromRgba(0, 0, 0, 0);

        for (var vertexIndex = 0; vertexIndex < VerticesPerTail; vertexIndex++)
        {
            meshData.AddVertexWithFlags(0f, radius, 0f, 0f, 0f, color, 0);
        }

        AddTailIndices(meshData, firstVertex);
    }

    private static void AddTailIndices(MeshData meshData, int firstVertex)
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

    private static SkyDirection Add(SkyDirection first, SkyDirection second)
        => new(first.X + second.X, first.Y + second.Y, first.Z + second.Z);

    private static SkyDirection Scale(SkyDirection value, double factor)
        => new(value.X * factor, value.Y * factor, value.Z * factor);

    private static SkyDirection Cross(SkyDirection first, SkyDirection second)
        => new(
            (first.Y * second.Z) - (first.Z * second.Y),
            (first.Z * second.X) - (first.X * second.Z),
            (first.X * second.Y) - (first.Y * second.X));

    private static SkyDirection Normalize(SkyDirection direction)
    {
        var length = Math.Sqrt(
            (direction.X * direction.X) +
            (direction.Y * direction.Y) +
            (direction.Z * direction.Z));
        if (length <= double.Epsilon)
        {
            throw new ArgumentException("Comet tail directions must be non-zero.");
        }

        return Scale(direction, 1.0 / length);
    }
}
