using AstraTerra.Astronomy;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Turns a projected Milky Way band into one mesh, and therefore one draw call.
/// </summary>
/// <remarks>
/// The band's horizon fade rides on the vertices rather than on a shader uniform, because the fade
/// is different at every point of a mesh that spans the whole sky: the part overhead is at full
/// strength while the part setting in the west is already gone. One tint uniform could not say that.
/// <para>
/// The mesh is always built at full grid capacity even though the index list shrinks as the band
/// sets, for the same reason the star batches are: Vintage Story sizes a mesh's buffers from the
/// first upload and never grows them, so a band built while half of it was below the horizon would
/// have no room left when it rose.
/// </para>
/// </remarks>
public static class MilkyWayBandMeshBuilder
{
    public static MeshData Build(MilkyWayBand band, float radius)
    {
        ArgumentNullException.ThrowIfNull(band);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var meshData = new MeshData(
            Math.Max(1, band.Vertices.Count),
            Math.Max(1, band.IndexCapacity),
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        for (var index = 0; index < band.Vertices.Count; index++)
        {
            var vertex = band.Vertices[index];
            var alpha = (int)Math.Round(Math.Clamp(vertex.Alpha, 0f, 1f) * 255f);
            meshData.AddVertexWithFlags(
                (float)vertex.DirectionX * radius,
                (float)vertex.DirectionY * radius,
                (float)vertex.DirectionZ * radius,
                vertex.U,
                vertex.V,
                ColorUtil.ColorFromRgba(255, 255, 255, alpha),
                0);
        }

        for (var index = 0; index < band.Indices.Count; index++)
        {
            meshData.AddIndex(band.Indices[index]);
        }

        // The cells that were skipped for being below the horizon still take up their place in the
        // index buffer, as triangles with all three corners on the same vertex: no area, no fill,
        // and a buffer whose size does not change as the band rises.
        for (var index = band.Indices.Count; index < band.IndexCapacity; index++)
        {
            meshData.AddIndex(0);
        }

        return meshData;
    }
}
