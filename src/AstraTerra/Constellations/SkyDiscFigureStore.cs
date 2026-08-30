using System.Text.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstraTerra.Constellations;

/// <summary>
/// Where a disc keeps its figure.
/// </summary>
/// <remarks>
/// Itemstack attributes, next to the solar band and for the same reason: the figure is laid into
/// this disc, not written down by this player. A disc that changes hands arrives with somebody
/// else's constellation already on it, which is the whole point of engraving one.
/// </remarks>
public static class SkyDiscFigureStore
{
    public const string FigureJsonAttribute = "astraterraSkyDiscFigureJson";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static SkyDiscFigure? Read(ITreeAttribute? attributes)
    {
        var json = attributes?.GetString(FigureJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<FigureSnapshot>(json, SerializerOptions);
            return snapshot is null
                ? null
                : new SkyDiscFigure(snapshot.Edges.Select(edge => new ConstellationEdge(edge.A, edge.B, edge.Order)));
        }
        catch
        {
            // An unreadable figure is a blank disc rather than a broken one: the band on the same
            // disc is still worth reading, and a disc that throws while being drawn is a disc that
            // cannot be looked at at all.
            return null;
        }
    }

    public static SkyDiscFigure? Read(ItemStack? stack) => Read(stack?.Attributes);

    public static SkyDiscFigure ReadOrEmpty(ItemStack? stack) => Read(stack) ?? new SkyDiscFigure();

    public static bool HasFigure(ItemStack? stack) => Read(stack)?.IsBlank == false;

    public static void Write(ITreeAttribute attributes, SkyDiscFigure figure)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(figure);

        var snapshot = new FigureSnapshot(
            [.. figure.Edges.Select(edge => new EdgeSnapshot(edge.A, edge.B, edge.EdgeOrder))]);
        attributes.SetString(FigureJsonAttribute, JsonSerializer.Serialize(snapshot, SerializerOptions));
    }

    public static void Write(ItemStack stack, SkyDiscFigure figure)
    {
        ArgumentNullException.ThrowIfNull(stack);

        Write(stack.Attributes, figure);
    }

    private sealed record FigureSnapshot(IReadOnlyList<EdgeSnapshot> Edges)
    {
        public FigureSnapshot()
            : this([])
        {
        }
    }

    private sealed record EdgeSnapshot(int A, int B, long Order);
}
