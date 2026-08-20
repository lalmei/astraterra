using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SkyLyingStateTests
{
    public SkyLyingStateTests()
    {
        SkyLyingState.Reset();
    }

    [Fact]
    public void Begin_And_End_Toggle_The_Pose()
    {
        Assert.False(SkyLyingState.IsLying);

        SkyLyingState.Begin();
        Assert.True(SkyLyingState.IsLying);

        SkyLyingState.End();
        Assert.False(SkyLyingState.IsLying);
    }

    [Fact]
    public void Reset_Clears_A_Session_Left_Lying_Down()
    {
        SkyLyingState.Begin();

        SkyLyingState.Reset();

        Assert.False(SkyLyingState.IsLying);
    }
}
