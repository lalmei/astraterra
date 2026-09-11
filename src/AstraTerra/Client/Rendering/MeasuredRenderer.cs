using System.Diagnostics;
using AstraTerra.Astronomy;
using Vintagestory.API.Client;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Wraps one of AstraTerra's renderers and puts a clock on it, without the renderer knowing.
/// </summary>
/// <remarks>
/// A decorator rather than a line of timing code inside each pass, because the passes are the thing
/// under suspicion and instrumentation that lives inside them is instrumentation that can be
/// forgotten in an early return. Wrapping at registration times the whole call including every path
/// out of it, and a pass that returns immediately is then visible as one that costs nothing rather
/// than as one that was never measured.
/// </remarks>
public sealed class MeasuredRenderer : IRenderer
{
    private readonly IRenderer inner;
    private readonly string name;

    public MeasuredRenderer(IRenderer inner, string name)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentException.ThrowIfNullOrEmpty(name);
        this.inner = inner;
        this.name = name;
    }

    public double RenderOrder => inner.RenderOrder;

    public int RenderRange => inner.RenderRange;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            inner.OnRenderFrame(deltaTime, stage);
        }
        finally
        {
            RenderCostLog.Record(name, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    public void Dispose() => inner.Dispose();
}

/// <summary>
/// Measures the client frame itself, so every pass's milliseconds have something to be a share of.
/// </summary>
/// <remarks>
/// <para>
/// Registered at <see cref="EnumRenderStage.Before"/>, which runs once per frame ahead of everything
/// else, so the gap between two visits is the whole frame: render, present, and whatever the client
/// did in between. That is the number a player means by frame rate, and it is deliberately not the
/// <c>deltaTime</c> a renderer is handed -- that is the simulation step, which a capped or stalled
/// client does not spend evenly.
/// </para>
/// <para>
/// The first frame after a window opens is skipped rather than measured, because the timestamp it
/// would be measured against belongs to before the world was loaded or to before a pause.
/// </para>
/// </remarks>
public sealed class FrameCostRenderer : IRenderer
{
    /// <summary>Ahead of every AstraTerra pass, so a frame is opened before anything reports into it.</summary>
    public const double FrameCostRenderOrder = 0.0;

    /// <summary>Longer than this between frames and the gap is a pause or a load, not a slow frame.</summary>
    private const double ImplausibleFrameMilliseconds = 1000.0;

    private readonly ICoreClientAPI api;
    private long lastTimestamp;

    public FrameCostRenderer(ICoreClientAPI api)
    {
        this.api = api;
    }

    public double RenderOrder => FrameCostRenderOrder;

    public int RenderRange => 0;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage != EnumRenderStage.Before)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var previous = lastTimestamp;
        lastTimestamp = now;
        if (previous == 0)
        {
            return;
        }

        var frameMilliseconds = Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds;
        if (frameMilliseconds > ImplausibleFrameMilliseconds)
        {
            return;
        }

        // The sun wrapper is counted on whatever thread the engine asks from, and folded into the
        // frame here, on the render thread, so the log's own bookkeeping stays single-threaded.
        var (sunCalls, sunMilliseconds) = SunDelegateCost.Drain();
        if (sunCalls > 0)
        {
            RenderCostLog.Record("AstraTerraSunDelegate", sunMilliseconds, sunCalls);
        }

        if (RenderCostLog.TryTakeReport(frameMilliseconds, out var report))
        {
            api.Logger.VerboseDebug("AstraTerra frame cost: {0}", RenderCostLog.Describe(report));
        }
    }

    public void Dispose()
    {
        lastTimestamp = 0;
        RenderCostLog.Reset();
    }
}
