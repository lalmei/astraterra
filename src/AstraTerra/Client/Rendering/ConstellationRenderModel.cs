using AstraTerra.Astronomy;
using AstraTerra.Constellations;

namespace AstraTerra.Client.Rendering;

public sealed record ConstellationSegment(
    int ConstellationId,
    int StartHip,
    int EndHip,
    RenderedStar Start,
    RenderedStar End);

public readonly record struct ConstellationTint(float R, float G, float B, float A);

public sealed record SkyConstellationDot(
    int ConstellationId,
    int StartHip,
    int EndHip,
    ConstellationTint Tint,
    double DirectionX,
    double DirectionY,
    double DirectionZ);

public static class ConstellationRenderModel
{
    private const float ConstellationTintAlpha = 0.34f;

    private static readonly ConstellationTint[] MochaPastelPalette =
    [
        new(0.537f, 0.863f, 0.922f, ConstellationTintAlpha), // Sky #89dceb
        new(0.706f, 0.745f, 0.996f, ConstellationTintAlpha), // Lavender #b4befe
        new(0.580f, 0.886f, 0.835f, ConstellationTintAlpha), // Teal #94e2d5
        new(0.651f, 0.890f, 0.631f, ConstellationTintAlpha), // Green #a6e3a1
        new(0.976f, 0.886f, 0.686f, ConstellationTintAlpha), // Yellow #f9e2af
        new(0.980f, 0.702f, 0.529f, ConstellationTintAlpha), // Peach #fab387
        new(0.961f, 0.761f, 0.906f, ConstellationTintAlpha), // Pink #f5c2e7
        new(0.796f, 0.651f, 0.969f, ConstellationTintAlpha), // Mauve #cba6f7
        new(0.961f, 0.878f, 0.862f, ConstellationTintAlpha), // Rosewater #f5e0dc
        new(0.455f, 0.780f, 0.925f, ConstellationTintAlpha), // Sapphire #74c7ec
        new(0.537f, 0.706f, 0.980f, ConstellationTintAlpha), // Blue #89b4fa
        new(0.949f, 0.804f, 0.804f, ConstellationTintAlpha), // Flamingo #f2cdcd
    ];

    public static IReadOnlyList<ConstellationSegment> BuildConstellationSegments(
        IEnumerable<ConstellationRecord> records,
        IReadOnlyDictionary<int, RenderedStar> visibleStarsByHip)
    {
        return records
            .SelectMany(record => record.Edges.Select(edge => new { Record = record, Edge = edge }))
            .Where(item => visibleStarsByHip.ContainsKey(item.Edge.A) && visibleStarsByHip.ContainsKey(item.Edge.B))
            .Select(item => new ConstellationSegment(
                item.Record.Id,
                item.Edge.A,
                item.Edge.B,
                visibleStarsByHip[item.Edge.A],
                visibleStarsByHip[item.Edge.B]))
            .ToList();
    }

    public static ConstellationTint GetConstellationTint(int constellationId)
    {
        var index = constellationId % MochaPastelPalette.Length;
        if (index < 0)
        {
            index += MochaPastelPalette.Length;
        }

        return MochaPastelPalette[index];
    }

    public static IReadOnlyList<SkyConstellationDot> BuildSkyDots(
        IEnumerable<ConstellationSegment> segments,
        double angularSpacingDeg)
    {
        if (angularSpacingDeg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(angularSpacingDeg), "Angular spacing must be positive.");
        }

        return segments.SelectMany(segment => BuildSkyDots(segment, angularSpacingDeg)).ToList();
    }

    private static IEnumerable<SkyConstellationDot> BuildSkyDots(ConstellationSegment segment, double angularSpacingDeg)
    {
        var angularDistanceDeg = AngularDistanceDeg(segment.Start, segment.End);
        if (angularDistanceDeg <= 0.000001)
        {
            yield break;
        }

        var tint = GetConstellationTint(segment.ConstellationId);
        var interiorDotCount = Math.Max(1, (int)Math.Ceiling(angularDistanceDeg / angularSpacingDeg) - 1);
        for (var i = 1; i <= interiorDotCount; i++)
        {
            var t = i / (double)(interiorDotCount + 1);
            var x = Lerp(segment.Start.DirectionX, segment.End.DirectionX, t);
            var y = Lerp(segment.Start.DirectionY, segment.End.DirectionY, t);
            var z = Lerp(segment.Start.DirectionZ, segment.End.DirectionZ, t);
            var length = Math.Sqrt((x * x) + (y * y) + (z * z));
            if (length <= 0.000001)
            {
                continue;
            }

            yield return new SkyConstellationDot(
                segment.ConstellationId,
                segment.StartHip,
                segment.EndHip,
                tint,
                x / length,
                y / length,
                z / length);
        }
    }

    private static double AngularDistanceDeg(RenderedStar start, RenderedStar end)
    {
        var dot = (start.DirectionX * end.DirectionX) + (start.DirectionY * end.DirectionY) + (start.DirectionZ * end.DirectionZ);
        return Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
    }

    private static double Lerp(double start, double end, double t)
    {
        return start + ((end - start) * t);
    }
}
