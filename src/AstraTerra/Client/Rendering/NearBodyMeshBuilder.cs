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
    /// <summary>Cells a body's own globe gets across it, whatever else shares the face.</summary>
    public const int CellsAcrossGlobe = 56;

    /// <summary>Fewest and most cells across a whole face.</summary>
    public const int MinSubdivisions = 24;
    public const int MaxSubdivisions = 192;

    /// <summary>
    /// How finely a face is cut up. The terminator is drawn by shading the corners of these cells,
    /// so a cell is the smallest step the terminator can take -- and a ringed giant's globe is only a
    /// third of its face, the rest being the room the rings need. Counting cells across the whole
    /// face therefore leaves barely six across the globe, and the terminator comes out as a staircase
    /// of blocks. The count is taken from the globe instead, and the rings get whatever that implies.
    /// </summary>
    public static int SubdivisionsFor(double discFraction)
    {
        var wanted = CellsAcrossGlobe / Math.Clamp(discFraction, 0.05, 1.0);
        return (int)Math.Clamp(Math.Ceiling(wanted), MinSubdivisions, MaxSubdivisions);
    }

    public static int VerticesFor(int subdivisions) => (subdivisions + 1) * (subdivisions + 1);

    public static int IndicesFor(int subdivisions) => subdivisions * subdivisions * 6;

    /// <summary>Light on the unlit side: a planet is never perfectly black against the sky.</summary>
    public const float NightSideLight = 0.055f;

    /// <summary>
    /// How far the sphere's normal is allowed to tip away from the observer at the limb.
    /// </summary>
    /// <remarks>
    /// A sphere's normal turns square to the eye exactly at its edge, and it does it with an
    /// infinite gradient: over the last cell of any grid, however fine, the facing term falls from
    /// a fraction to nothing. Shading the corners of cells cannot follow that, so the last ring of
    /// cells came out a scalloped fringe -- the planet's edge drawn with teeth. Holding the normal
    /// a little short of square costs a sliver of limb darkening that nobody can see and removes
    /// the cliff that nobody could draw.
    /// <para>
    /// Brightness only. Lifting the facing term where the body is drawn solid lifts the unlit rim
    /// with it, and the planet's silhouette comes back as a ring drawn round the sky.
    /// </para>
    /// </remarks>
    public const double LimbNormalFloor = 0.35;

    /// <summary>
    /// How far past the terminator the light fades, in units of the Lambert term. Wide enough that
    /// the fade always spans several cells, so what is left of the staircase is blurred out by it.
    /// A gas giant's terminator is genuinely soft anyway: it is an atmosphere, not a cliff.
    /// </summary>
    public const float TerminatorSoftness = 0.16f;

    /// <summary>
    /// How much brighter than the sky a point has to be to draw solid against it in daylight.
    /// </summary>
    /// <remarks>
    /// By day a body is fogged out in proportion to how little light it sends, and a lit face sends
    /// plenty even where its own limb darkening has taken a third of it. Without this gain that
    /// third came off the silhouette instead, and ate a translucent ring out of the planet's edge.
    /// </remarks>
    public const float DaylightGain = 1.45f;

    public static MeshData Build(
        IReadOnlyList<PlacedNearBody> bodies,
        float radius,
        int capacity,
        double daylight = 0.0)
    {
        ArgumentNullException.ThrowIfNull(bodies);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        var subdivisions = bodies.Count > 0
            ? bodies.Max(body => SubdivisionsFor(body.Body.Face.DiscFraction))
            : MinSubdivisions;
        var meshData = new MeshData(
            Math.Max(1, capacity * VerticesFor(subdivisions)),
            Math.Max(1, capacity * IndicesFor(subdivisions)),
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
            Add(meshData, bodies[index], radius, Math.Clamp(daylight, 0.0, 1.0), subdivisions);
        }

        for (var slot = drawn; slot < capacity; slot++)
        {
            AddUnusedSlot(meshData, radius, subdivisions);
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

        var toward = Math.Max(LimbNormalFloor, Math.Sqrt(Math.Max(0.0, 1.0 - distanceSquared)));
        var lambert = (faceX * sunRight) + (faceY * sunUp) + (toward * sunToward);
        var lit = Math.Clamp((lambert + TerminatorSoftness) / (2.0 * TerminatorSoftness), 0.0, 1.0);

        // Limb darkening, in the same term: the edge of a lit disc is never as bright as its middle.
        var limb = 0.72 + (0.28 * toward);
        return (float)Math.Clamp(NightSideLight + ((1.0 - NightSideLight) * lit * limb), 0.0, 1.0);
    }

    private static void Add(MeshData meshData, PlacedNearBody placed, float radius, double daylight, int subdivisions)
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

        for (var row = 0; row <= subdivisions; row++)
        {
            var v = (float)row / subdivisions;
            var offsetUp = (v * 2f) - 1f;
            for (var column = 0; column <= subdivisions; column++)
            {
                var u = (float)column / subdivisions;
                var offsetRight = (u * 2f) - 1f;
                var light = LightAt(
                    offsetRight / discFraction,
                    offsetUp / discFraction,
                    sunRight,
                    sunUp,
                    sunToward,
                    placed.IlluminatedFraction) * brightness;
                var shade = (int)Math.Clamp(light * 255f, 0f, 255f);

                // Opaque at night, so the disc occults the stars behind it. By day it is fogged out
                // in proportion to how little light it sends: the lit face saturates and stays
                // solid to its edge, the night side all but vanishes, and the terminator grades
                // between them. Nothing here asks whether a point is lit -- that question has a
                // cliff at the limb, and answering it drew the planet as a ring round the sky.
                var opacity = (int)Math.Clamp(
                    ((light * DaylightGain) + (1.0 - daylight)) * 255.0,
                    0.0,
                    255.0);

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

        AddGridIndices(meshData, firstVertex, subdivisions);
    }

    private static void AddUnusedSlot(MeshData meshData, float radius, int subdivisions)
    {
        var firstVertex = meshData.VerticesCount;
        for (var vertex = 0; vertex < VerticesFor(subdivisions); vertex++)
        {
            meshData.AddVertexWithFlags(0f, radius, 0f, 0f, 0f, ColorUtil.ColorFromRgba(0, 0, 0, 0), 0);
        }

        AddGridIndices(meshData, firstVertex, subdivisions);
    }

    private static void AddGridIndices(MeshData meshData, int firstVertex, int subdivisions)
    {
        var stride = subdivisions + 1;
        for (var row = 0; row < subdivisions; row++)
        {
            for (var column = 0; column < subdivisions; column++)
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
