using AstraTerra.Astronomy;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Builds the mesh for one near body: a grid across its face, shaded per vertex by where the sun is.
/// </summary>
/// <remarks>
/// <para>
/// A star or a planet is a point of light and a single quad is the whole of it. A body wide enough
/// to show a disc is not: it has a terminator running across it, and that line is the difference
/// between a moon and a bright sticker. So the face is a grid rather than a quad, and each vertex is
/// lit by the sphere normal at that point -- the same Lambert term the real globe obeys, which
/// curves the terminator and darkens the limb on its own.
/// </para>
/// <para>
/// Vertices outside the globe are the ring plane, where there is no normal to speak of. Those take
/// the body's overall lit fraction instead, so a ring dims with its planet's phase rather than
/// blazing through the new-moon side.
/// </para>
/// <para>
/// A body's night side is drawn black rather than left out, because at night that is what it is: a
/// bite taken out of the star field. In daylight the same black disc would be a hole in a blue sky,
/// so the unlit part fades out as the day comes up and only the lit crescent is left -- which is all
/// a daytime moon has ever been.
/// </para>
/// <para>
/// The grid is gnomonic: corners are offsets along the two axes across the line of sight, scaled by
/// the tangent of the half-angle. That is exactly how a sphere's disc projects, so a body tens of
/// degrees wide stays round instead of stretching at its edges.
/// </para>
/// </remarks>
public static class NearBodyMeshBuilder
{
    /// <summary>Cells across the face. 24 keeps the terminator smooth at a body filling a quarter of the sky.</summary>
    public const int Subdivisions = 24;

    public const int VerticesPerBody = (Subdivisions + 1) * (Subdivisions + 1);
    public const int IndicesPerBody = Subdivisions * Subdivisions * 6;

    /// <summary>Light on the unlit side: a planet is never perfectly black against the sky.</summary>
    public const float NightSideLight = 0.055f;

    /// <summary>How far past the terminator the light fades, in units of the Lambert term.</summary>
    public const float TerminatorSoftness = 0.12f;

    public static MeshData Build(
        IReadOnlyList<PlacedNearBody> bodies,
        float radius,
        int capacity,
        double daylight = 0.0)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var meshData = new MeshData(
            Math.Max(1, capacity * VerticesPerBody),
            Math.Max(1, capacity * IndicesPerBody),
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        var drawn = Math.Min(bodies.Count, capacity);
        for (var index = 0; index < drawn; index++)
        {
            Add(meshData, bodies[index], radius, Math.Clamp(daylight, 0.0, 1.0));
        }

        for (var slot = drawn; slot < capacity; slot++)
        {
            AddUnusedSlot(meshData, radius);
        }

        return meshData;
    }

    /// <summary>
    /// The lit fraction at one point on the face, in face coordinates where the globe is the unit
    /// circle: the Lambert term of the sphere normal there, softened across the terminator.
    /// </summary>
    public static float LightAt(
        double faceX,
        double faceY,
        double sunRight,
        double sunUp,
        double sunToward,
        double illuminatedFraction)
    {
        var distanceSquared = (faceX * faceX) + (faceY * faceY);
        if (distanceSquared > 1.0)
        {
            // The ring plane: no normal of its own, so it follows the globe's phase.
            return (float)Math.Clamp(NightSideLight + (0.95 * illuminatedFraction), 0.0, 1.0);
        }

        var toward = Math.Sqrt(Math.Max(0.0, 1.0 - distanceSquared));
        var lambert = (faceX * sunRight) + (faceY * sunUp) + (toward * sunToward);
        var lit = Math.Clamp((lambert + TerminatorSoftness) / (2.0 * TerminatorSoftness), 0.0, 1.0);

        // Limb darkening, in the same term: the edge of a lit disc is never as bright as its middle.
        var limb = 0.72 + (0.28 * toward);
        return (float)Math.Clamp(NightSideLight + ((1.0 - NightSideLight) * lit * limb), 0.0, 1.0);
    }

    private static void Add(MeshData meshData, PlacedNearBody placed, float radius, double daylight)
    {
        var direction = placed.Direction;
        var (rightX, rightY, rightZ, upX, upY, upZ) = BuildFaceAxes(direction.X, direction.Y, direction.Z);
        var halfSize = radius * MathF.Tan((float)placed.AngularDiameterDeg * MathF.PI / 360f);
        var centerX = (float)direction.X * radius;
        var centerY = (float)direction.Y * radius;
        var centerZ = (float)direction.Z * radius;

        // The sun, resolved onto the face's own axes, is all the shading needs.
        var sun = placed.SunDirection;
        var sunRight = (sun.X * rightX) + (sun.Y * rightY) + (sun.Z * rightZ);
        var sunUp = (sun.X * upX) + (sun.Y * upY) + (sun.Z * upZ);
        var sunToward = -((sun.X * direction.X) + (sun.Y * direction.Y) + (sun.Z * direction.Z));

        var discFraction = Math.Clamp(placed.Body.Face.DiscFraction, 0.05, 1.0);
        var brightness = Math.Clamp(placed.Body.Brightness, 0.0, 1.0);
        var firstVertex = meshData.VerticesCount;

        for (var row = 0; row <= Subdivisions; row++)
        {
            var v = (float)row / Subdivisions;
            var offsetUp = (v * 2f) - 1f;
            for (var column = 0; column <= Subdivisions; column++)
            {
                var u = (float)column / Subdivisions;
                var offsetRight = (u * 2f) - 1f;
                var light = LightAt(
                    offsetRight / discFraction,
                    offsetUp / discFraction,
                    sunRight,
                    sunUp,
                    sunToward,
                    placed.IlluminatedFraction) * brightness;
                var shade = (int)Math.Clamp(light * 255f, 0f, 255f);

                // Opaque at night, so the disc occults the stars behind it; by day only what is lit
                // stands against the sky.
                var opacity = (int)Math.Clamp((light + (1.0 - daylight)) * 255.0, 0.0, 255.0);

                meshData.AddVertexWithFlags(
                    centerX + (rightX * halfSize * offsetRight) + (upX * halfSize * offsetUp),
                    centerY + (rightY * halfSize * offsetRight) + (upY * halfSize * offsetUp),
                    centerZ + (rightZ * halfSize * offsetRight) + (upZ * halfSize * offsetUp),
                    u,
                    1f - v,
                    ColorUtil.ColorFromRgba(shade, shade, shade, opacity),
                    0);
            }
        }

        AddGridIndices(meshData, firstVertex);
    }

    private static void AddUnusedSlot(MeshData meshData, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        for (var vertex = 0; vertex < VerticesPerBody; vertex++)
        {
            meshData.AddVertexWithFlags(0f, radius, 0f, 0f, 0f, ColorUtil.ColorFromRgba(0, 0, 0, 0), 0);
        }

        AddGridIndices(meshData, firstVertex);
    }

    private static void AddGridIndices(MeshData meshData, int firstVertex)
    {
        const int stride = Subdivisions + 1;
        for (var row = 0; row < Subdivisions; row++)
        {
            for (var column = 0; column < Subdivisions; column++)
            {
                var corner = firstVertex + (row * stride) + column;
                meshData.AddIndex(corner);
                meshData.AddIndex(corner + 1);
                meshData.AddIndex(corner + stride + 1);
                meshData.AddIndex(corner);
                meshData.AddIndex(corner + stride + 1);
                meshData.AddIndex(corner + stride);
            }
        }
    }

    /// <summary>
    /// Two axes across the line of sight, the same pair <see cref="SkyBillboardMeshBuilder"/> builds:
    /// world up as the reference, and world north where that degenerates overhead.
    /// </summary>
    private static (float RightX, float RightY, float RightZ, float UpX, float UpY, float UpZ) BuildFaceAxes(
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
