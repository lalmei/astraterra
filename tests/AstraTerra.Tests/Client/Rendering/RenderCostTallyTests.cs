using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The line that decides whether a frame-rate complaint is AstraTerra's to answer, so the share it
/// reports has to be the share the frame actually spent.
/// </summary>
public sealed class RenderCostTallyTests
{
    [Fact]
    public void A_Window_Reports_The_Frame_Rate_It_Measured()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        // Fifty frames of twenty milliseconds is one second at fifty frames a second.
        for (var frame = 0; frame < 49; frame++)
        {
            Assert.False(tally.TryTakeReport(20.0, out _));
        }

        Assert.True(tally.TryTakeReport(20.0, out var report));
        Assert.Equal(50, report.Frames);
        Assert.Equal(50.0, report.FramesPerSecond, 6);
        Assert.Equal(20.0, report.AverageFrameMilliseconds, 6);
    }

    [Fact]
    public void A_Pass_Costs_The_Frame_Only_As_Often_As_It_Runs()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        // Ten milliseconds, but only in one frame of ten: a tenth of a millisecond per frame, which
        // is what the frame rate feels. Reporting it as ten would blame a pass that is asleep.
        for (var frame = 0; frame < 100; frame++)
        {
            if (frame % 10 == 0)
            {
                tally.Record("nightOnly", 10.0);
            }

            tally.TryTakeReport(10.0, out _);
        }

        Assert.True(tally.Latest is not null);
        var pass = Assert.Single(tally.Latest!.Value.Passes);
        Assert.Equal("nightOnly", pass.Name);
        Assert.Equal(10, pass.DrawnFrames);
        Assert.Equal(1.0, pass.MillisecondsPerClientFrame, 6);
        Assert.Equal(10.0, pass.PeakMilliseconds, 6);
    }

    [Fact]
    public void The_Share_Is_Every_Pass_Against_The_Whole_Frame()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        for (var frame = 0; frame < 100; frame++)
        {
            tally.Record("sky", 1.0);
            tally.Record("nearBodies", 2.0);
            tally.TryTakeReport(10.0, out _);
        }

        var report = tally.Latest!.Value;
        Assert.Equal(3.0, report.ModMillisecondsPerFrame, 6);
        Assert.Equal(0.3, report.ModShareOfFrame, 6);
    }

    [Fact]
    public void A_Peak_Survives_Its_Window_And_Not_The_Next_One()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        tally.Record("sky", 40.0);
        for (var frame = 0; frame < 100; frame++)
        {
            tally.Record("sky", 1.0);
            tally.TryTakeReport(10.0, out _);
        }

        Assert.Equal(40.0, tally.Latest!.Value.Passes[0].PeakMilliseconds, 6);

        for (var frame = 0; frame < 100; frame++)
        {
            tally.Record("sky", 1.0);
            tally.TryTakeReport(10.0, out _);
        }

        Assert.Equal(1.0, tally.Latest!.Value.Passes[0].PeakMilliseconds, 6);
    }

    [Fact]
    public void Engine_Driven_Work_Reports_How_Often_It_Was_Called()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        // The sun delegate is one frame's cost but many calls, and the call count is the point: the
        // arithmetic is cheap and only the multiplier can make it expensive.
        for (var frame = 0; frame < 100; frame++)
        {
            tally.Record("sun", 0.5, calls: 2000);
            tally.TryTakeReport(10.0, out _);
        }

        var pass = Assert.Single(tally.Latest!.Value.Passes);
        Assert.Equal(100, pass.DrawnFrames);
        Assert.Equal(2000.0, pass.CallsPerFrame, 6);
        Assert.Equal(0.5, pass.MillisecondsPerClientFrame, 6);
        Assert.Contains("2000 calls/frame", RenderCostLog.Describe(tally.Latest!.Value));
    }

    [Fact]
    public void A_Pass_Called_Once_A_Frame_Does_Not_Say_So()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        for (var frame = 0; frame < 100; frame++)
        {
            tally.Record("sky", 0.5);
            tally.TryTakeReport(10.0, out _);
        }

        Assert.DoesNotContain("calls/frame", RenderCostLog.Describe(tally.Latest!.Value));
    }

    [Fact]
    public void A_Pass_That_Never_Ran_Is_Not_Invented()
    {
        var tally = new RenderCostTally(intervalSeconds: 1.0);

        for (var frame = 0; frame < 100; frame++)
        {
            tally.TryTakeReport(10.0, out _);
        }

        var report = tally.Latest!.Value;
        Assert.Empty(report.Passes);
        Assert.Equal(0.0, report.ModShareOfFrame, 6);
        Assert.Contains("none", RenderCostLog.Describe(report));
    }
}
