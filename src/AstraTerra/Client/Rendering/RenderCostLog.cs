namespace AstraTerra.Client.Rendering;

/// <summary>What one named render pass cost over a reporting window.</summary>
/// <param name="Name">The pass, as it is registered with Vintage Story.</param>
/// <param name="DrawnFrames">Frames the pass actually did work in; a pass that bailed early counts none.</param>
/// <param name="MillisecondsPerClientFrame">
/// Total time in the pass divided by every client frame in the window, drawn or not. This is the
/// number that can be laid against the frame budget: a pass that is expensive but only runs a
/// tenth of the time is a tenth as expensive to the frame rate.
/// </param>
/// <param name="PeakMilliseconds">The worst single visit, which is what a player feels as a hitch.</param>
/// <param name="CallsPerFrame">
/// How many times the work ran per client frame. One for a render pass, which is called once a
/// frame by definition; for the sun delegate, which the engine calls from wherever it wants the
/// sun, this is the number that decides whether cheap arithmetic adds up to an expensive frame.
/// </param>
public readonly record struct RenderPassCost(
    string Name,
    int DrawnFrames,
    double MillisecondsPerClientFrame,
    double PeakMilliseconds,
    double CallsPerFrame = 1.0);

/// <summary>What a whole client frame cost, and how much of it was AstraTerra's.</summary>
/// <param name="Frames">Client frames in the window.</param>
/// <param name="Seconds">Wall-clock length of the window, so frames per second is honest.</param>
/// <param name="AverageFrameMilliseconds">Mean wall time from one frame's start to the next.</param>
/// <param name="PeakFrameMilliseconds">Longest frame in the window.</param>
/// <param name="Passes">Every instrumented pass, in registration order.</param>
public readonly record struct RenderCostReport(
    int Frames,
    double Seconds,
    double AverageFrameMilliseconds,
    double PeakFrameMilliseconds,
    IReadOnlyList<RenderPassCost> Passes)
{
    /// <summary>Frames per second over the window.</summary>
    public double FramesPerSecond => Seconds <= 0.0 ? 0.0 : Frames / Seconds;

    /// <summary>Every AstraTerra pass added together, per client frame.</summary>
    public double ModMillisecondsPerFrame => Passes.Sum(static pass => pass.MillisecondsPerClientFrame);

    /// <summary>AstraTerra's share of the frame, 0 to 1. The number the whole exercise is for.</summary>
    public double ModShareOfFrame
        => AverageFrameMilliseconds <= 0.0 ? 0.0 : ModMillisecondsPerFrame / AverageFrameMilliseconds;
}

/// <summary>
/// Times every AstraTerra render pass against the frame it is spending, so "the mod costs me frames"
/// stops being a guess.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SkyPassMetrics"/> already answered this for the star pass, and answered it well: that
/// pass costs a fraction of a millisecond. What it could not say is whether that fraction is the
/// whole of AstraTerra or a tenth of it, because the mod registers nine renderers and only one of
/// them was ever on a clock. A pass measured on its own can only ever report that it is cheap; it
/// takes the frame budget next to it to say whether cheap matters.
/// </para>
/// <para>
/// So every pass reports here, and the frame clock reports the frame itself. One line then carries
/// the only comparison worth making: milliseconds of AstraTerra against milliseconds of frame. If
/// the mod is one millisecond of thirty, the frame rate is somebody else's business and no amount of
/// optimising sky code will move it.
/// </para>
/// <para>
/// Per-frame cost is divided by every client frame rather than by the frames the pass drew in,
/// because a pass that runs only at night and costs two milliseconds when it does is not costing a
/// daytime frame anything. Both numbers are reported: the mean is what the frame rate feels, and
/// the drawn-frame count says how often the pass was awake at all.
/// </para>
/// </remarks>
public sealed class RenderCostTally
{
    private readonly List<string> order = [];
    private readonly Dictionary<string, Accumulator> passes = new(StringComparer.Ordinal);
    private readonly double intervalSeconds;
    private int frames;
    private double windowSeconds;
    private double totalFrameMilliseconds;
    private double peakFrameMilliseconds;

    public RenderCostTally(double intervalSeconds = RenderCostLog.ReportIntervalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intervalSeconds);
        this.intervalSeconds = intervalSeconds;
    }

    /// <summary>The most recent finished window, for a command that wants to print it on demand.</summary>
    public RenderCostReport? Latest { get; private set; }

    /// <summary>Records one visit to a pass. Called from the render thread only.</summary>
    public void Record(string name, double milliseconds) => Record(name, milliseconds, calls: 1);

    /// <summary>
    /// Records one frame's worth of work that ran <paramref name="calls"/> times inside it.
    /// </summary>
    /// <remarks>
    /// The overload exists for work the engine drives rather than the render loop: it is still one
    /// frame's cost, but it was not one visit, and reporting it as one would hide the only thing
    /// worth knowing about it.
    /// </remarks>
    public void Record(string name, double milliseconds, long calls)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (!passes.TryGetValue(name, out var accumulator))
        {
            accumulator = new Accumulator();
            passes[name] = accumulator;
            order.Add(name);
        }

        accumulator.DrawnFrames++;
        accumulator.TotalMilliseconds += milliseconds;
        accumulator.TotalCalls += calls;
        accumulator.PeakMilliseconds = Math.Max(accumulator.PeakMilliseconds, milliseconds);
    }

    /// <summary>
    /// Closes a client frame and, once the window is up, hands back what it cost and starts a new one.
    /// </summary>
    /// <param name="frameMilliseconds">
    /// Wall time from this frame's start to the next one's. Measured rather than taken from the
    /// engine's delta, because the delta a renderer is handed is the simulation step and a capped or
    /// stalled client does not spend it evenly.
    /// </param>
    public bool TryTakeReport(double frameMilliseconds, out RenderCostReport report)
    {
        frames++;
        totalFrameMilliseconds += frameMilliseconds;
        peakFrameMilliseconds = Math.Max(peakFrameMilliseconds, frameMilliseconds);
        windowSeconds += frameMilliseconds / 1000.0;

        if (windowSeconds < intervalSeconds)
        {
            report = default;
            return false;
        }

        var costs = new List<RenderPassCost>(order.Count);
        foreach (var name in order)
        {
            var accumulator = passes[name];
            costs.Add(new RenderPassCost(
                name,
                accumulator.DrawnFrames,
                frames == 0 ? 0.0 : accumulator.TotalMilliseconds / frames,
                accumulator.PeakMilliseconds,
                frames == 0 ? 0.0 : accumulator.TotalCalls / (double)frames));
        }

        report = new RenderCostReport(
            frames,
            windowSeconds,
            frames == 0 ? 0.0 : totalFrameMilliseconds / frames,
            peakFrameMilliseconds,
            costs);
        Latest = report;
        ResetWindow();
        return true;
    }

    public void Reset()
    {
        order.Clear();
        passes.Clear();
        Latest = null;
        ResetWindow();
    }

    private void ResetWindow()
    {
        frames = 0;
        windowSeconds = 0;
        totalFrameMilliseconds = 0;
        peakFrameMilliseconds = 0;
        foreach (var accumulator in passes.Values)
        {
            accumulator.DrawnFrames = 0;
            accumulator.TotalMilliseconds = 0;
            accumulator.TotalCalls = 0;
            accumulator.PeakMilliseconds = 0;
        }
    }

    private sealed class Accumulator
    {
        public int DrawnFrames;
        public double TotalMilliseconds;
        public long TotalCalls;
        public double PeakMilliseconds;
    }
}

/// <summary>
/// The one tally the client's render passes report into, and the line they are reported as.
/// </summary>
/// <remarks>
/// Static because the passes it measures are spread across nine registrations and a Harmony patch
/// that has no instance to hang anything on. The counting itself lives in <see cref="RenderCostTally"/>
/// so it can be tested without a running client.
/// </remarks>
public static class RenderCostLog
{
    /// <summary>Matches the sky pass's own reporting window, so the two lines line up in the log.</summary>
    public const double ReportIntervalSeconds = 30.0;

    private static readonly RenderCostTally Tally = new();

    /// <summary>The most recent finished window, for a command that wants to print it on demand.</summary>
    public static RenderCostReport? Latest => Tally.Latest;

    public static void Record(string name, double milliseconds) => Tally.Record(name, milliseconds);

    public static void Record(string name, double milliseconds, long calls)
        => Tally.Record(name, milliseconds, calls);

    public static bool TryTakeReport(double frameMilliseconds, out RenderCostReport report)
        => Tally.TryTakeReport(frameMilliseconds, out report);

    public static void Reset() => Tally.Reset();

    /// <summary>One line carrying the comparison: what the frame cost, and what of it was ours.</summary>
    public static string Describe(RenderCostReport report)
    {
        var passes = report.Passes.Count == 0
            ? "none"
            : string.Join(
                "; ",
                report.Passes
                    .OrderByDescending(static pass => pass.MillisecondsPerClientFrame)
                    .Select(static pass =>
                    {
                        // Call count is only worth the width when it is not simply once a frame.
                        var calls = pass.CallsPerFrame > 1.5
                            ? $", {pass.CallsPerFrame:0} calls/frame"
                            : string.Empty;
                        return $"{pass.Name}={pass.MillisecondsPerClientFrame:0.000}ms (peak {pass.PeakMilliseconds:0.00}, drew {pass.DrawnFrames}{calls})";
                    }));

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "fps={0:0.0}; frame={1:0.00}ms (peak {2:0.0}); astraterra={3:0.000}ms ({4:0.0}% of frame); {5}",
            report.FramesPerSecond,
            report.AverageFrameMilliseconds,
            report.PeakFrameMilliseconds,
            report.ModMillisecondsPerFrame,
            report.ModShareOfFrame * 100.0,
            passes);
    }

}
