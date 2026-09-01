using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Constellations;

public enum SkyDiscEngraveOutcome
{
    /// <summary>The line went on.</summary>
    Engraved,

    /// <summary>That line is already on this disc.</summary>
    AlreadyThere,

    /// <summary>A line has to run between two stars.</summary>
    SameStar,

    /// <summary>The material holds no figure at all.</summary>
    NotEngravable,

    /// <summary>Fired clay: it holds the figure it was fired with, and no more.</summary>
    TooHard,

    /// <summary>No loose flint or knife is ready in the hotbar to cut the line.</summary>
    NoScribingTool,

    /// <summary>A second figure. A disc holds one.</summary>
    NoRoom,
}

public sealed record SkyDiscEngraveResult(SkyDiscEngraveOutcome Outcome, string Message)
{
    public bool Changed => Outcome == SkyDiscEngraveOutcome.Engraved;
}

/// <summary>
/// The one figure a disc carries: a single joined-up shape, laid into the metal.
/// </summary>
/// <remarks>
/// A journal book is a notebook and holds as many constellations as its owner draws. A disc is not:
/// it is one object with one face, and what goes on it is the one figure its owner thought worth
/// carrying. So the rule is not a count of lines but a shape — every line has to join the figure
/// already there. A line struck somewhere else on the sky would be a second constellation, and
/// there is no room for it.
/// <para>
/// Which is also why the rule is enforced here rather than by counting: two figures are two
/// unconnected pieces, and that is a question about the graph, not about how much has been drawn.
/// </para>
/// </remarks>
public sealed class SkyDiscFigure
{
    /// <summary>What a disc says when a line would start a second figure.</summary>
    public const string NoRoomMessage = "The disc only has room for one constellation.";

    private readonly List<ConstellationEdge> edges;
    private long nextEdgeOrder;

    public SkyDiscFigure()
        : this([])
    {
    }

    public SkyDiscFigure(IEnumerable<ConstellationEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(edges);

        this.edges = edges.Select(edge => edge.Normalize()).ToList();
        nextEdgeOrder = this.edges.Count == 0 ? 1 : this.edges.Max(edge => edge.EdgeOrder) + 1;
    }

    public IReadOnlyList<ConstellationEdge> Edges => edges;

    public bool IsBlank => edges.Count == 0;

    /// <summary>The stars this figure is hung on.</summary>
    public IReadOnlyCollection<int> Stars => edges.SelectMany(edge => new[] { edge.A, edge.B }).ToHashSet();

    /// <summary>
    /// Lays one line between two stars into the disc, if the disc will take it.
    /// </summary>
    /// <remarks>
    /// Nothing is written unless the answer is <see cref="SkyDiscEngraveOutcome.Engraved"/>, so a
    /// refusal leaves the figure exactly as it was and can be reported and forgotten.
    /// </remarks>
    public SkyDiscEngraveResult Engrave(
        int startHip,
        int endHip,
        int figuresAllowed,
        bool workable = true,
        bool hasScribingTool = true)
    {
        if (figuresAllowed <= 0)
        {
            return new SkyDiscEngraveResult(
                SkyDiscEngraveOutcome.NotEngravable,
                "This disc will not hold a figure.");
        }

        if (!workable)
        {
            return new SkyDiscEngraveResult(
                SkyDiscEngraveOutcome.TooHard,
                "The clay is fired hard. A figure goes into it before it is baked, not after.");
        }

        if (!hasScribingTool)
        {
            return new SkyDiscEngraveResult(
                SkyDiscEngraveOutcome.NoScribingTool,
                SkyDiscScribingTool.MissingMessage);
        }

        if (startHip == endHip)
        {
            return new SkyDiscEngraveResult(
                SkyDiscEngraveOutcome.SameStar,
                "A line runs between two stars.");
        }

        if (edges.Any(edge => edge.Matches(startHip, endHip)))
        {
            return new SkyDiscEngraveResult(
                SkyDiscEngraveOutcome.AlreadyThere,
                "That line is already on the disc.");
        }

        // The figure is what is already joined up. A line that touches none of it is a second one.
        if (edges.Count > 0 && !Stars.Contains(startHip) && !Stars.Contains(endHip))
        {
            return new SkyDiscEngraveResult(SkyDiscEngraveOutcome.NoRoom, NoRoomMessage);
        }

        edges.Add(new ConstellationEdge(startHip, endHip, nextEdgeOrder++).Normalize());
        return new SkyDiscEngraveResult(
            SkyDiscEngraveOutcome.Engraved,
            edges.Count == 1
                ? "The first line of a figure goes into the disc."
                : "Another line joins the figure on the disc.");
    }

    /// <summary>Takes a line off again, for a figure that went wrong.</summary>
    /// <remarks>
    /// Removing may leave the figure in two pieces, which is a state <see cref="Engrave"/> would
    /// never have allowed. That is the observer's problem to finish, not the disc's to refuse: the
    /// alternative is a figure that can be drawn into a corner it cannot be drawn out of.
    /// </remarks>
    public bool Erase(int startHip, int endHip)
        => edges.RemoveAll(edge => edge.Matches(startHip, endHip)) > 0;

    /// <summary>Whether this figure is one joined-up shape, which is the only shape it may be.</summary>
    public bool IsOnePiece => ConstellationGraph.SplitConnectedComponents(edges).Count <= 1;

    /// <summary>
    /// The figure as a constellation, so the drawing that puts a book's constellations in the sky
    /// can put a disc's figure there too without knowing which it was handed.
    /// </summary>
    public ConstellationRecord AsRecord(string? name = null)
        => new(SkyDiscEngraving.FigureId, name, 0, 0, edges);
}
