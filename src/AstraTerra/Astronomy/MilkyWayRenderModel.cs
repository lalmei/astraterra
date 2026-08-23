namespace AstraTerra.Astronomy;

/// <summary>One corner of the Milky Way band: where it points, where it reads the glow map, and how much of it survives the horizon.</summary>
public readonly record struct MilkyWayVertex(
    double DirectionX,
    double DirectionY,
    double DirectionZ,
    float U,
    float V,
    float Alpha);

/// <summary>
/// The band as drawable geometry: a grid of vertices, the triangles worth drawing, and how many
/// triangles the full grid would need if the whole band were above the horizon.
/// </summary>
public sealed record MilkyWayBand(
    IReadOnlyList<MilkyWayVertex> Vertices,
    IReadOnlyList<int> Indices,
    int IndexCapacity);

/// <summary>
/// Places the unresolved glow of the galaxy on an observer's sky.
/// </summary>
/// <remarks>
/// Every other sky object in the mod is a position: a star, a plate, a comet, each projected one at
/// a time. The Milky Way is not a position at all — it is the light of the stars the catalog stops
/// short of, and it covers the whole sphere at once. So it is drawn as the sphere: a grid in
/// galactic coordinates, rotated into the equatorial frame once per vertex and then onto the
/// observer's horizon, with the glow map wrapped over it.
/// <para>
/// The grid is deliberately coarse. Nothing in the shape of the band lives in the geometry — the
/// rift, the clouds and the bulge are all in the texture, which is sampled per pixel — so the mesh
/// only has to be fine enough that a great circle does not visibly become a polygon, and fine
/// enough that the horizon fade is smooth along it.
/// </para>
/// <para>
/// The horizon handling is the starfield's, with one difference: the cutoff sits much closer to the
/// true horizon than a star's does. A star below the terrain is a point that is either drawn or not,
/// but the band is continuous, so anything drawn below the horizon arrives as a glow lying over the
/// hills rather than behind them.
/// </para>
/// </remarks>
public static class MilkyWayRenderModel
{
    /// <summary>Grid columns around the galactic equator.</summary>
    public const int DefaultLongitudeSteps = 72;

    /// <summary>Grid rows from the north galactic pole to the south.</summary>
    public const int DefaultLatitudeSteps = 36;

    /// <summary>Altitude at which the band reaches full brightness, fading in below it.</summary>
    public const double DefaultHorizonFadeBandDeg = 14.0;

    /// <summary>Altitude below which the band is not drawn at all.</summary>
    public const double DefaultVisualHorizonCutoffDeg = -3.0;

    /// <summary>
    /// Builds the band for an observer at a latitude and a sidereal angle.
    /// </summary>
    /// <remarks>
    /// Cells whose four corners are all below the cutoff are left out of the index list rather than
    /// drawn transparent. Half the sphere is under the observer's feet at any moment, and a cell
    /// that is not indexed costs no fill — which is the expensive half of a mesh that covers the sky.
    /// </remarks>
    public static MilkyWayBand ProjectBand(
        double latitudeDeg,
        double localSiderealDeg,
        int longitudeSteps = DefaultLongitudeSteps,
        int latitudeSteps = DefaultLatitudeSteps,
        double horizonFadeBandDeg = DefaultHorizonFadeBandDeg,
        double visualHorizonCutoffDeg = DefaultVisualHorizonCutoffDeg)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(longitudeSteps, 4);
        ArgumentOutOfRangeException.ThrowIfLessThan(latitudeSteps, 2);

        var columns = longitudeSteps + 1;
        var vertices = new MilkyWayVertex[columns * (latitudeSteps + 1)];
        var indices = new List<int>(longitudeSteps * latitudeSteps * 6);

        for (var row = 0; row <= latitudeSteps; row++)
        {
            var v = row / (float)latitudeSteps;
            var galacticLatitude = 90.0 - (180.0 * row / latitudeSteps);
            for (var column = 0; column <= longitudeSteps; column++)
            {
                // The texture's first column is galactic longitude +180 and its last is -180, the
                // same direction, so the seam column is a duplicate vertex carrying u = 1 rather
                // than a wrap back to u = 0. Wrapping instead would run the whole map backwards
                // across one cell.
                var u = column / (float)longitudeSteps;
                var galacticLongitude = 180.0 - (360.0 * column / longitudeSteps);
                var equatorial = GalacticFrame.ToEquatorial(galacticLongitude, galacticLatitude);
                var horizontal = CelestialMath.GetHorizontalCoordinates(
                    equatorial.RightAscensionDeg,
                    equatorial.DeclinationDeg,
                    latitudeDeg,
                    localSiderealDeg);
                var (x, y, z) = SkyProjection.GetWorldDirection(horizontal.AzimuthDeg, horizontal.AltitudeDeg);
                var alpha = horizontal.AltitudeDeg <= visualHorizonCutoffDeg
                    ? 0f
                    : (float)SkyProjection.GetHorizonFadeFactor(
                        horizontal.AltitudeDeg,
                        horizonFadeBandDeg,
                        visualHorizonCutoffDeg);

                vertices[(row * columns) + column] = new MilkyWayVertex(x, y, z, u, v, alpha);
            }
        }

        for (var row = 0; row < latitudeSteps; row++)
        {
            for (var column = 0; column < longitudeSteps; column++)
            {
                var topLeft = (row * columns) + column;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + columns;
                var bottomRight = bottomLeft + 1;
                if (vertices[topLeft].Alpha <= 0f &&
                    vertices[topRight].Alpha <= 0f &&
                    vertices[bottomLeft].Alpha <= 0f &&
                    vertices[bottomRight].Alpha <= 0f)
                {
                    continue;
                }

                indices.Add(topLeft);
                indices.Add(topRight);
                indices.Add(bottomRight);
                indices.Add(topLeft);
                indices.Add(bottomRight);
                indices.Add(bottomLeft);
            }
        }

        return new MilkyWayBand(vertices, indices, longitudeSteps * latitudeSteps * 6);
    }
}
