namespace AstraTerra.Client.Rendering;

/// <summary>
/// What the sky pass actually cost, over the window between two diagnostic log lines.
/// </summary>
/// <param name="Frames">Frames the sky pass drew in this window.</param>
/// <param name="AverageMilliseconds">Mean time inside the pass.</param>
/// <param name="PeakMilliseconds">Worst frame, which is the one a player feels as a stutter.</param>
/// <param name="AverageDrawCalls">Mean <c>RenderMesh</c> calls issued per drawing frame.</param>
/// <param name="PeakDrawCalls">Worst frame's draw calls.</param>
/// <param name="MeshUploads">New meshes uploaded in this window.</param>
/// <param name="MeshUpdates">Existing meshes rewritten in this window.</param>
public readonly record struct SkyPassReport(
    int Frames,
    double AverageMilliseconds,
    double PeakMilliseconds,
    double AverageDrawCalls,
    int PeakDrawCalls,
    int MeshUploads,
    int MeshUpdates);

/// <summary>
/// Counts the GL work the sky pass issues, so "is it faster?" has an answer from inside the game.
/// </summary>
/// <remarks>
/// The diagnostic line used to report list lengths — how many stars were visible, how many dots were
/// built — which happened to equal draw calls only until the dots were batched. These counters follow
/// the calls themselves, so the numbers stay true across batching work rather than quietly becoming
/// a lie about what the GPU was asked to do.
/// <para>
/// Peaks are reported next to means because the two answer different questions: a mean tells you what
/// the pass costs, a peak tells you what the player felt.
/// </para>
/// </remarks>
public sealed class SkyPassMetrics
{
    private int frames;
    private double totalMilliseconds;
    private double peakMilliseconds;
    private int totalDrawCalls;
    private int peakDrawCalls;
    private int meshUploads;
    private int meshUpdates;
    private int frameDrawCalls;
    private double windowSeconds;

    /// <summary>Draw calls issued so far in the frame being measured.</summary>
    public int FrameDrawCalls => frameDrawCalls;

    public void BeginFrame() => frameDrawCalls = 0;

    public void CountDrawCall() => frameDrawCalls++;

    public void CountMeshUpload() => meshUploads++;

    public void CountMeshUpdate() => meshUpdates++;

    /// <summary>Closes a frame the sky pass drew in. Frames it skipped are not counted.</summary>
    public void EndFrame(double elapsedMilliseconds)
    {
        frames++;
        totalMilliseconds += elapsedMilliseconds;
        peakMilliseconds = Math.Max(peakMilliseconds, elapsedMilliseconds);
        totalDrawCalls += frameDrawCalls;
        peakDrawCalls = Math.Max(peakDrawCalls, frameDrawCalls);
    }

    /// <summary>
    /// Advances the window clock and, once <paramref name="intervalSeconds"/> has passed, hands back
    /// what the window cost and starts a new one.
    /// </summary>
    /// <remarks>
    /// Driven by the caller's frame delta rather than a wall clock, so a paused or throttled client
    /// does not report a window it never drew.
    /// </remarks>
    public bool TryTakeReport(double deltaSeconds, double intervalSeconds, out SkyPassReport report)
    {
        windowSeconds += deltaSeconds;
        if (windowSeconds < intervalSeconds)
        {
            report = default;
            return false;
        }

        report = new SkyPassReport(
            frames,
            frames == 0 ? 0.0 : totalMilliseconds / frames,
            peakMilliseconds,
            frames == 0 ? 0.0 : totalDrawCalls / (double)frames,
            peakDrawCalls,
            meshUploads,
            meshUpdates);

        ResetWindow();
        return true;
    }

    public void Reset()
    {
        ResetWindow();
        windowSeconds = 0;
        frameDrawCalls = 0;
    }

    private void ResetWindow()
    {
        windowSeconds = 0;
        frames = 0;
        totalMilliseconds = 0;
        peakMilliseconds = 0;
        totalDrawCalls = 0;
        peakDrawCalls = 0;
        meshUploads = 0;
        meshUpdates = 0;
    }
}
