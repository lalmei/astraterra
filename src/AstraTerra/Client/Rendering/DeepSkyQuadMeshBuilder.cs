using AstraTerra.Astronomy;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public static class DeepSkyQuadMeshBuilder
{
    public const int DefaultSubdivisions = 8;

    public static MeshData Build(
        IReadOnlyList<DeepSkyDirection> corners,
        float radius,
        int subdivisions = DefaultSubdivisions)
    {
        if (corners.Count != 4)
        {
            throw new ArgumentException("A deep-sky spherical quad requires exactly four corners.", nameof(corners));
        }

        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (subdivisions < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(subdivisions));
        }

        var rowSize = subdivisions + 1;
        var meshData = new MeshData(
            rowSize * rowSize,
            subdivisions * subdivisions * 6,
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        for (var row = 0; row <= subdivisions; row++)
        {
            var catalogV = row / (double)subdivisions;
            var left = Normalize(Lerp(corners[0], corners[3], catalogV));
            var right = Normalize(Lerp(corners[1], corners[2], catalogV));
            // Stellarium flips decoded image rows before uploading them to OpenGL,
            // while Vintage Story uploads the PNG rows as-is. Invert V at this
            // boundary so Stellarium's bottom-origin texture coordinates retain
            // their registered sky positions in Vintage Story.
            var engineV = 1.0 - catalogV;
            for (var column = 0; column <= subdivisions; column++)
            {
                var u = column / (double)subdivisions;
                var direction = Normalize(Lerp(left, right, u));
                meshData.AddVertexWithFlags(
                    (float)direction.X * radius,
                    (float)direction.Y * radius,
                    (float)direction.Z * radius,
                    (float)u,
                    (float)engineV,
                    ColorUtil.WhiteArgb,
                    0);
            }
        }

        for (var row = 0; row < subdivisions; row++)
        {
            for (var column = 0; column < subdivisions; column++)
            {
                var topLeft = (row * rowSize) + column;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + rowSize;
                var bottomRight = bottomLeft + 1;
                meshData.AddIndex(topLeft);
                meshData.AddIndex(topRight);
                meshData.AddIndex(bottomRight);
                meshData.AddIndex(topLeft);
                meshData.AddIndex(bottomRight);
                meshData.AddIndex(bottomLeft);
            }
        }

        return meshData;
    }

    private static DeepSkyDirection Lerp(DeepSkyDirection start, DeepSkyDirection end, double amount)
        => new(
            start.X + ((end.X - start.X) * amount),
            start.Y + ((end.Y - start.Y) * amount),
            start.Z + ((end.Z - start.Z) * amount));

    private static DeepSkyDirection Normalize(DeepSkyDirection direction)
    {
        var length = Math.Sqrt(
            (direction.X * direction.X) +
            (direction.Y * direction.Y) +
            (direction.Z * direction.Z));
        if (length <= double.Epsilon)
        {
            throw new ArgumentException("Deep-sky quad corners must define non-zero directions.", nameof(direction));
        }

        return new DeepSkyDirection(direction.X / length, direction.Y / length, direction.Z / length);
    }
}
