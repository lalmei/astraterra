using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>One sprite on the sky sphere: where it points, how wide it is, and what colour it draws.</summary>
public readonly record struct SkyBillboard(
    double DirectionX,
    double DirectionY,
    double DirectionZ,
    float AngularSizeDeg,
    int Color);

/// <summary>
/// Bakes sky sprites into one mesh, so a thousand of them cost one draw call.
/// </summary>
/// <remarks>
/// Sky billboards do not face the camera's look direction: they face the player, who sits at the
/// centre of the sky sphere. Turning your head therefore cannot invalidate the geometry, which is
/// what lets a batch survive across frames and be rebuilt only when the sky itself turns.
/// <para>
/// Callers group by texture before building — the sprites in play at any moment are few, so one mesh
/// per sprite replaces thousands of single-quad draws without needing a texture atlas.
/// </para>
/// </remarks>
public static class SkyBillboardMeshBuilder
{
    public const int VerticesPerBillboard = 4;
    public const int IndicesPerBillboard = 6;

    /// <summary>
    /// Builds a mesh holding <paramref name="capacity"/> sprites' worth of geometry, whatever the
    /// sky currently shows.
    /// </summary>
    /// <remarks>
    /// Always built at full capacity: Vintage Story sizes a mesh's GPU buffers from the vertex count
    /// of the first <c>UploadMesh</c> and never grows them, while <c>UpdateMesh</c> still advertises
    /// the new index count to the draw call. Unused slots collapse to a transparent point, so they
    /// cost no fill.
    /// </remarks>
    public static MeshData Build(IReadOnlyList<SkyBillboard> billboards, float radius, int capacity)
    {
        ArgumentNullException.ThrowIfNull(billboards);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var meshData = new MeshData(
            Math.Max(1, capacity * VerticesPerBillboard),
            Math.Max(1, capacity * IndicesPerBillboard),
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        var drawn = Math.Min(billboards.Count, capacity);
        for (var index = 0; index < drawn; index++)
        {
            Add(meshData, billboards[index], radius);
        }

        for (var slot = drawn; slot < capacity; slot++)
        {
            AddUnusedSlot(meshData, radius);
        }

        return meshData;
    }

    /// <summary>Sprites needed for <paramref name="count"/>, rounded up so the mesh is not resized constantly.</summary>
    public static int GetCapacityFor(int count, int growthStep = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(growthStep);

        return ((Math.Max(count, 1) + growthStep - 1) / growthStep) * growthStep;
    }

    private static void Add(MeshData meshData, SkyBillboard billboard, float radius)
    {
        var centerX = (float)billboard.DirectionX * radius;
        var centerY = (float)billboard.DirectionY * radius;
        var centerZ = (float)billboard.DirectionZ * radius;
        var halfSize = radius * MathF.Tan(billboard.AngularSizeDeg * MathF.PI / 360f);

        var (rightX, rightY, rightZ, upX, upY, upZ) = BuildBillboardAxes(
            billboard.DirectionX,
            billboard.DirectionY,
            billboard.DirectionZ);
        var firstVertex = meshData.VerticesCount;

        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, -1f, -1f, 0f, 0f, billboard.Color);
        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, 1f, -1f, 1f, 0f, billboard.Color);
        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, 1f, 1f, 1f, 1f, billboard.Color);
        AddCorner(meshData, centerX, centerY, centerZ, rightX, rightY, rightZ, upX, upY, upZ, halfSize, -1f, 1f, 0f, 1f, billboard.Color);

        AddQuadIndices(meshData, firstVertex);
    }

    private static void AddUnusedSlot(MeshData meshData, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        for (var corner = 0; corner < VerticesPerBillboard; corner++)
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

    /// <summary>
    /// Two axes across the line of sight. World up is the reference, except directly overhead or
    /// underfoot where it degenerates and world north stands in.
    /// </summary>
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
}
