using System.Diagnostics;

namespace AstraTerra.Astronomy;

/// <summary>
/// Counts what the longitude-aware sun wrapper costs, because it is the one thing AstraTerra does
/// that no render pass can see.
/// </summary>
/// <remarks>
/// <para>
/// Every other cost this mod carries is inside a renderer, and a renderer can be wrapped and timed.
/// This one is not: the wrapper is handed to Vintage Story's calendar and called by the engine,
/// from wherever the engine wants the sun -- lighting, the sky, weather, shadows, an entity's shade.
/// So it is spent inside somebody else's frame, and a mod that measured only its own passes would
/// report itself blameless no matter how expensive this got.
/// </para>
/// <para>
/// What matters is not the cost of one call but how many calls there are. The arithmetic is a dozen
/// trig operations and a world-config lookup, which is nothing once and something else entirely at
/// a few thousand times a frame. So calls are counted alongside the time, and the report gives both.
/// </para>
/// <para>
/// The counters are interlocked and drained by the render thread rather than written into the
/// render log directly, because the calendar is called from more than one thread and the log's
/// dictionary is not built for that. Counting is off until a client switches it on: the server owns
/// a wrapper too, and nothing on that side has a frame to report into.
/// </para>
/// </remarks>
public static class SunDelegateCost
{
    private static long calls;
    private static long elapsedTicks;
    private static volatile bool enabled;

    /// <summary>Whether calls are being counted. Switched on by the client installer only.</summary>
    public static bool IsEnabled => enabled;

    public static void Enable() => enabled = true;

    /// <summary>Times one call into the wrapper. The timestamp is the caller's, taken before the work.</summary>
    public static void Record(long startTimestamp)
    {
        if (!enabled)
        {
            return;
        }

        Interlocked.Increment(ref calls);
        Interlocked.Add(ref elapsedTicks, Stopwatch.GetTimestamp() - startTimestamp);
    }

    /// <summary>Takes everything counted since the last drain and resets to zero.</summary>
    public static (long Calls, double Milliseconds) Drain()
    {
        var drainedCalls = Interlocked.Exchange(ref calls, 0);
        var drainedTicks = Interlocked.Exchange(ref elapsedTicks, 0);
        return (drainedCalls, drainedTicks * 1000.0 / Stopwatch.Frequency);
    }

    public static void Reset()
    {
        enabled = false;
        Interlocked.Exchange(ref calls, 0);
        Interlocked.Exchange(ref elapsedTicks, 0);
    }
}
