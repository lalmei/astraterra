using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Batches every constellation dot into the single mesh the sky pass draws.
/// </summary>
/// <remarks>
/// The dots are not decoration beside the lines — they <em>are</em> the lines, one billboard every
/// 0.65 deg along each edge, which on the authored catalogue is around 3700 of them. Drawn one at a
/// time that was more draw calls than the whole star catalogue, every frame, for as long as a player
/// carried a journal.
/// <para>
/// They can be batched because a sky billboard does not face the camera's look direction: it faces
/// the player, who sits at the centre of the sky sphere. Turning your head therefore changes nothing
/// about where these quads point, so one mesh survives until the sky itself turns.
/// </para>
/// <para>
/// Vertex colour carries the tint that used to be a per-dot uniform, which is what collapses the
/// batch to a single draw call: every dot shares one texture.
/// </para>
/// </remarks>
public static class ConstellationDotMeshBuilder
{
    public const int VerticesPerDot = 4;
    public const int IndicesPerDot = 6;

    /// <summary>
    /// Builds a mesh holding <paramref name="capacity"/> dots' worth of geometry, whatever the
    /// journal currently shows.
    /// </summary>
    /// <remarks>
    /// Always built at full capacity, for the reason
    /// <see cref="MeteorStreakMeshBuilder"/> documents: Vintage Story sizes a mesh's GPU buffers from
    /// the vertex count of the first <c>UploadMesh</c> and never grows them, while <c>UpdateMesh</c>
    /// still advertises the new index count to the draw call. Unused slots are collapsed to a point
    /// and fully transparent, so they cost no fill.
    /// </remarks>
    /// <param name="radius">Distance to the sky sphere the dots sit on.</param>
    /// <param name="angularSizeDeg">Width of one dot, in degrees of sky.</param>
    public static MeshData Build(
        IReadOnlyList<SkyConstellationDot> dots,
        float radius,
        float angularSizeDeg,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(dots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(angularSizeDeg);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var meshData = new MeshData(
            Math.Max(1, capacity * VerticesPerDot),
            Math.Max(1, capacity * IndicesPerDot),
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        var halfSize = radius * MathF.Tan(angularSizeDeg * MathF.PI / 360f);
        var drawn = Math.Min(dots.Count, capacity);
        for (var index = 0; index < drawn; index++)
        {
            AddDot(meshData, dots[index], radius, halfSize);
        }

        for (var slot = drawn; slot < capacity; slot++)
        {
            AddUnusedDotSlot(meshData, radius);
        }

        return meshData;
    }

    /// <summary>Dots needed for <paramref name="dotCount"/>, rounded up so the mesh is not resized for every edit.</summary>
    public static int GetCapacityFor(int dotCount, int growthStep = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dotCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(growthStep);

        return ((Math.Max(dotCount, 1) + growthStep - 1) / growthStep) * growthStep;
    }

    private static void AddDot(MeshData meshData, SkyConstellationDot dot, float radius, float halfSize)
    {
        var centerX = (float)dot.DirectionX * radius;
        var centerY = (float)dot.DirectionY * radius;
        var centerZ = (float)dot.DirectionZ * radius;

        // Any two axes across the line of sight will do: the sprite is a round dot, so its roll about
        // that line is not visible. World up is the reference, except directly overhead or underfoot
        // where it degenerates and world north stands in.
        var (rightX, rightY, rightZ, upX, upY, upZ) = BuildBillboardAxes(dot.DirectionX, dot.DirectionY, dot.DirectionZ);
        var color = ColorFromTint(dot.Tint);
        var firstVertex = meshData.VerticesCount;

        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, -1f, -1f, 0f, 0f, color);
        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, 1f, -1f, 1f, 0f, color);
        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, 1f, 1f, 1f, 1f, color);
        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, -1f, 1f, 0f, 1f, color);

        AddQuadIndices(meshData, firstVertex);
    }

    /// <summary>
    /// Fills one dot's worth of the batch with nothing: four coincident, fully transparent vertices,
    /// so the slot draws no pixels but keeps the index buffer the size it was allocated at.
    /// </summary>
    private static void AddUnusedDotSlot(MeshData meshData, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        for (var corner = 0; corner < VerticesPerDot; corner++)
        {
            meshData.AddVertexWithFlags(0f, radius, 0f, 0f, 0f, ColorUtil.ColorFromRgba(0, 0, 0, 0), 0);
        }

        AddQuadIndices(meshData, firstVertex);
    }

    private static void AddCorner(
        MeshData meshData,
        float centerX,
        float centerY,
        float centerZ,
        float rightX,
        float rightY,
        float rightZ,
        float upX,
        float upY,
        float upZ,
        float halfSize,
        float rightSign,
        float upSign,
        float u,
        float v,
        int color)
    {
        meshData.AddVertexWithFlags(
            centerX + (rightX * halfSize * rightSign) + (upX * halfSize * upSign),
            centerY + (rightY * halfSize * rightSign) + (upY * halfSize * upSign),
            centerZ + (rightZ * halfSize * rightSign) + (upZ * halfSize * upSign),
            u,
            v,
            color,
            0);
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

    private static (float RightX, float RightY, float RightZ, float UpX, float UpY, float UpZ) BuildBillboardAxes(
        double directionX,
        double directionY,
        double directionZ)
    {
        var referenceX = 0.0;
        var referenceY = 1.0;
        var referenceZ = 0.0;
        if (Math.Abs(directionY) > 0.999)
        {
            referenceY = 0.0;
            referenceZ = -1.0;
        }

        var rightX = (referenceY * directionZ) - (referenceZ * directionY);
        var rightY = (referenceZ * directionX) - (referenceX * directionZ);
        var rightZ = (referenceX * directionY) - (referenceY * directionX);
        var rightLength = Math.Sqrt((rightX * rightX) + (rightY * rightY) + (rightZ * rightZ));
        if (rightLength < 1e-9)
        {
            return (1f, 0f, 0f, 0f, 1f, 0f);
        }

        rightX /= rightLength;
        rightY /= rightLength;
        rightZ /= rightLength;

        var upX = (directionY * rightZ) - (directionZ * rightY);
        var upY = (directionZ * rightX) - (directionX * rightZ);
        var upZ = (directionX * rightY) - (directionY * rightX);

        return ((float)rightX, (float)rightY, (float)rightZ, (float)upX, (float)upY, (float)upZ);
    }

    private static int ColorFromTint(ConstellationTint tint)
        => ColorUtil.ColorFromRgba(
            ToByte(tint.R),
            ToByte(tint.G),
            ToByte(tint.B),
            ToByte(tint.A));

    private static int ToByte(float channel) => (int)Math.Round(Math.Clamp(channel, 0f, 1f) * 255f);
}
