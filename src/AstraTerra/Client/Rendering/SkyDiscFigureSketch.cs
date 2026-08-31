using AstraTerra.Astronomy;
using AstraTerra.Constellations;

namespace AstraTerra.Client.Rendering;

/// <summary>One engraved line after a constellation has been fitted to the middle of a disc.</summary>
public readonly record struct SkyDiscSketchLine(double StartX, double StartY, double EndX, double EndY);

/// <summary>One star punched into the disc at the end of an engraved line.</summary>
public readonly record struct SkyDiscSketchStar(int Hip, double X, double Y);

/// <summary>
/// Projects the stars of a disc's one constellation onto its flat face.
/// </summary>
/// <remarks>
/// A figure stores catalog ids rather than pixels, because those same ids have to put it back on
/// the sky. The disc therefore needs a small chart projection of its own. It is centred on the
/// figure's average direction, keeps celestial north upright, and is scaled as one shape into the
/// clear face inside the rim. Cartesian directions avoid the seam at zero right ascension, where a
/// plain minimum/maximum of sky coordinates would tear a perfectly ordinary figure in two.
/// </remarks>
public sealed record SkyDiscFigureSketch(
    IReadOnlyList<SkyDiscSketchLine> Lines,
    IReadOnlyList<SkyDiscSketchStar> Stars)
{
    /// <summary>How far a fitted figure may reach from the centre of the disc, in shape units.</summary>
    public const double Radius = 2.65;

    public static SkyDiscFigureSketch Empty { get; } = new([], []);

    public static SkyDiscFigureSketch Project(SkyDiscFigure? figure, StarCatalog? catalog)
        => Project(
            figure,
            catalog?.Stars.ToDictionary(star => star.Hip)
                ?? (IReadOnlyDictionary<int, StarCatalogEntry>)new Dictionary<int, StarCatalogEntry>());

    /// <summary>
    /// Projects against a catalog already indexed by id. The live item renderer keeps this index
    /// for the session rather than rebuilding a ten-thousand-star dictionary every frame.
    /// </summary>
    public static SkyDiscFigureSketch Project(
        SkyDiscFigure? figure,
        IReadOnlyDictionary<int, StarCatalogEntry> catalogByHip)
    {
        ArgumentNullException.ThrowIfNull(catalogByHip);

        if (figure is null || figure.IsBlank || catalogByHip.Count == 0)
        {
            return Empty;
        }

        var edges = figure.Edges
            .Where(edge => catalogByHip.ContainsKey(edge.A) && catalogByHip.ContainsKey(edge.B))
            .ToList();
        if (edges.Count == 0)
        {
            return Empty;
        }

        var starIds = edges
            .SelectMany(edge => new[] { edge.A, edge.B })
            .Distinct()
            .OrderBy(hip => hip)
            .ToList();
        var directions = starIds.ToDictionary(hip => hip, hip => Direction(catalogByHip[hip]));

        var centre = Normalize(
            directions.Values.Sum(vector => vector.X),
            directions.Values.Sum(vector => vector.Y),
            directions.Values.Sum(vector => vector.Z));
        if (centre.Length < 1e-9)
        {
            centre = directions[starIds[0]];
        }

        // Celestial north projected into the tangent plane keeps north at the top of the sketch.
        // At the pole that direction is undefined, so the zero-RA meridian supplies a stable one.
        var northDot = centre.Z;
        var north = Normalize(
            -northDot * centre.X,
            -northDot * centre.Y,
            1.0 - (northDot * centre.Z));
        if (north.Length < 1e-9)
        {
            north = Normalize(1.0 - (centre.X * centre.X), -centre.X * centre.Y, -centre.X * centre.Z);
        }

        var east = Normalize(
            (north.Y * centre.Z) - (north.Z * centre.Y),
            (north.Z * centre.X) - (north.X * centre.Z),
            (north.X * centre.Y) - (north.Y * centre.X));

        var projected = directions.ToDictionary(
            pair => pair.Key,
            pair => (
                X: -Dot(pair.Value, east), // Sky charts conventionally put increasing RA to the left.
                Y: Dot(pair.Value, north)));
        var middleX = (projected.Values.Min(point => point.X) + projected.Values.Max(point => point.X)) / 2.0;
        var middleY = (projected.Values.Min(point => point.Y) + projected.Values.Max(point => point.Y)) / 2.0;
        var extent = projected.Values.Max(point => Math.Max(Math.Abs(point.X - middleX), Math.Abs(point.Y - middleY)));
        if (extent < 1e-9)
        {
            return Empty;
        }

        var scale = Radius / extent;
        var fitted = projected.ToDictionary(
            pair => pair.Key,
            pair => (X: (pair.Value.X - middleX) * scale, Y: (pair.Value.Y - middleY) * scale));
        var lines = edges
            .Select(edge => new SkyDiscSketchLine(
                fitted[edge.A].X,
                fitted[edge.A].Y,
                fitted[edge.B].X,
                fitted[edge.B].Y))
            .Where(line => Math.Abs(line.StartX - line.EndX) > 1e-9 || Math.Abs(line.StartY - line.EndY) > 1e-9)
            .ToList();
        if (lines.Count == 0)
        {
            return Empty;
        }

        var stars = starIds.Select(hip => new SkyDiscSketchStar(hip, fitted[hip].X, fitted[hip].Y)).ToList();

        return new SkyDiscFigureSketch(lines, stars);
    }

    /// <summary>A compact identity for the stored graph, used by the per-item mesh cache.</summary>
    public static string Key(SkyDiscFigure? figure)
        => figure is null || figure.IsBlank
            ? "blank"
            : string.Join(",", figure.Edges.Select(edge => $"{edge.A}-{edge.B}"));

    private static Direction3 Direction(StarCatalogEntry star)
    {
        var rightAscension = star.RightAscensionDeg * Math.PI / 180.0;
        var declination = star.DeclinationDeg * Math.PI / 180.0;
        var cosDeclination = Math.Cos(declination);
        return new Direction3(
            cosDeclination * Math.Cos(rightAscension),
            cosDeclination * Math.Sin(rightAscension),
            Math.Sin(declination),
            1.0);
    }

    private static Direction3 Normalize(double x, double y, double z)
    {
        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return length < 1e-12
            ? new Direction3(0.0, 0.0, 0.0, 0.0)
            : new Direction3(x / length, y / length, z / length, length);
    }

    private static double Dot(Direction3 left, Direction3 right)
        => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private readonly record struct Direction3(double X, double Y, double Z, double Length);
}
