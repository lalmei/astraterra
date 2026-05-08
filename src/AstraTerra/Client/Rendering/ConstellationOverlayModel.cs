using AstraTerra.Astronomy;
using AstraTerra.Constellations;

namespace AstraTerra.Client.Rendering;

public sealed record ScreenConstellationSegment(
    int ConstellationId,
    int StartHip,
    int EndHip,
    RenderedStar Start,
    RenderedStar End);

public static class ConstellationOverlayModel
{
    public static IReadOnlyList<ScreenConstellationSegment> BuildConstellationSegments(
        IEnumerable<ConstellationRecord> records,
        IReadOnlyDictionary<int, RenderedStar> visibleStarsByHip)
    {
        return records
            .SelectMany(record => record.Edges.Select(edge => new { Record = record, Edge = edge }))
            .Where(item => visibleStarsByHip.ContainsKey(item.Edge.A) && visibleStarsByHip.ContainsKey(item.Edge.B))
            .Select(item => new ScreenConstellationSegment(
                item.Record.Id,
                item.Edge.A,
                item.Edge.B,
                visibleStarsByHip[item.Edge.A],
                visibleStarsByHip[item.Edge.B]))
            .ToList();
    }
}
