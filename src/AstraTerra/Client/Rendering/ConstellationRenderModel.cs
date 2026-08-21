using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed record ConstellationSegment(
    int ConstellationId,
    int StartHip,
    int EndHip,
    RenderedStar Start,
    RenderedStar End);

public readonly record struct ConstellationTint(float R, float G, float B, float A);

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

    /// <summary>
    /// Turns the edges of a journal's patterns into the ribbons the sky pass draws, one per edge.
    /// </summary>
    public static IReadOnlyList<SkyConstellationLine> BuildSkyLines(IEnumerable<ConstellationSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var lines = new List<SkyConstellationLine>();
        foreach (var segment in segments)
        {
            // Two stars in the same place have no line between them, and no direction to build one
            // along either.
            if (AngularDistanceDeg(segment.Start, segment.End) <= 0.000001)
            {
                continue;
            }

            lines.Add(new SkyConstellationLine(
                segment.Start.DirectionX,
                segment.Start.DirectionY,
                segment.Start.DirectionZ,
                segment.End.DirectionX,
                segment.End.DirectionY,
                segment.End.DirectionZ,
                ColorFromTint(GetConstellationTint(segment.ConstellationId))));
        }

        return lines;
    }

    private static int ColorFromTint(ConstellationTint tint)
        => ColorUtil.ColorFromRgba(
            ToByte(tint.R),
            ToByte(tint.G),
            ToByte(tint.B),
            ToByte(tint.A));

    private static int ToByte(float channel) => (int)Math.Round(Math.Clamp(channel, 0f, 1f) * 255f);

    private static double AngularDistanceDeg(RenderedStar start, RenderedStar end)
    {
        var dot = (start.DirectionX * end.DirectionX) + (start.DirectionY * end.DirectionY) + (start.DirectionZ * end.DirectionZ);
        return Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
    }
}
