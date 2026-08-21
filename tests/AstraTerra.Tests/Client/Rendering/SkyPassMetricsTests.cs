using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The numbers a player reads back in a support thread, so they have to mean what they say.
/// </summary>
public sealed class SkyPassMetricsTests
{
    [Fact]
    public void A_Window_Reports_Mean_And_Peak_Of_The_Frames_It_Saw()
    {
        var metrics = new SkyPassMetrics();

        Frame(metrics, milliseconds: 2.0, drawCalls: 10);
        Frame(metrics, milliseconds: 8.0, drawCalls: 30);

        Assert.True(metrics.TryTakeReport(deltaSeconds: 30.0, intervalSeconds: 30.0, out var report));
        Assert.Equal(2, report.Frames);
        Assert.Equal(5.0, report.AverageMilliseconds, 6);
        Assert.Equal(8.0, report.PeakMilliseconds, 6);
        Assert.Equal(20.0, report.AverageDrawCalls, 6);
        Assert.Equal(30, report.PeakDrawCalls);
    }

    [Fact]
    public void Nothing_Is_Reported_Before_The_Window_Closes()
    {
        var metrics = new SkyPassMetrics();
        Frame(metrics, 4.0, 12);

        Assert.False(metrics.TryTakeReport(deltaSeconds: 10.0, intervalSeconds: 30.0, out _));
        Assert.False(metrics.TryTakeReport(deltaSeconds: 10.0, intervalSeconds: 30.0, out _));
        Assert.True(metrics.TryTakeReport(deltaSeconds: 10.0, intervalSeconds: 30.0, out var report));
        Assert.Equal(1, report.Frames);
    }

    [Fact]
    public void Each_Window_Starts_Clean()
    {
        var metrics = new SkyPassMetrics();
        Frame(metrics, 9.0, 40);
        Assert.True(metrics.TryTakeReport(30.0, 30.0, out _));

        Frame(metrics, 1.0, 2);
        Assert.True(metrics.TryTakeReport(30.0, 30.0, out var second));

        Assert.Equal(1, second.Frames);
        Assert.Equal(1.0, second.AverageMilliseconds, 6);
        Assert.Equal(1.0, second.PeakMilliseconds, 6);
        Assert.Equal(2, second.PeakDrawCalls);
    }

    [Fact]
    public void Draw_Calls_Are_Counted_Per_Frame_Not_Cumulatively()
    {
        var metrics = new SkyPassMetrics();

        metrics.BeginFrame();
        metrics.CountDrawCall();
        metrics.CountDrawCall();
        Assert.Equal(2, metrics.FrameDrawCalls);
        metrics.EndFrame(1.0);

        metrics.BeginFrame();
        Assert.Equal(0, metrics.FrameDrawCalls);
    }

    [Fact]
    public void Uploads_And_Updates_Are_Counted_Across_The_Window()
    {
        var metrics = new SkyPassMetrics();

        metrics.BeginFrame();
        metrics.CountMeshUpload();
        metrics.EndFrame(1.0);
        metrics.BeginFrame();
        metrics.CountMeshUpdate();
        metrics.CountMeshUpdate();
        metrics.EndFrame(1.0);

        Assert.True(metrics.TryTakeReport(30.0, 30.0, out var report));
        Assert.Equal(1, report.MeshUploads);
        Assert.Equal(2, report.MeshUpdates);
    }

    /// <summary>
    /// A window the sky never drew in — a whole day, say — reports zero rather than dividing by it.
    /// </summary>
    [Fact]
    public void A_Window_With_No_Drawing_Frames_Reports_Zero()
    {
        var metrics = new SkyPassMetrics();

        Assert.True(metrics.TryTakeReport(30.0, 30.0, out var report));
        Assert.Equal(0, report.Frames);
        Assert.Equal(0.0, report.AverageMilliseconds);
        Assert.Equal(0.0, report.AverageDrawCalls);
    }

    private static void Frame(SkyPassMetrics metrics, double milliseconds, int drawCalls)
    {
        metrics.BeginFrame();
        for (var call = 0; call < drawCalls; call++)
        {
            metrics.CountDrawCall();
        }

        metrics.EndFrame(milliseconds);
    }
}
