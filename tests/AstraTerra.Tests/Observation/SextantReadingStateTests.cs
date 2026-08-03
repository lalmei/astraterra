using AstraTerra.Config;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SextantReadingStateTests
{
    [Fact]
    public void CycleGridMode_Cycles_Angle_Equatorial_Azimuthal_And_Both()
    {
        SextantReadingState.Reset();

        Assert.Equal(SkyGridMode.None, SextantReadingState.GridMode);
        Assert.Equal(SkyGridMode.Equatorial, SextantReadingState.CycleGridMode());
        Assert.Equal(SkyGridMode.Horizontal, SextantReadingState.CycleGridMode());
        Assert.Equal(SkyGridMode.Both, SextantReadingState.CycleGridMode());
        Assert.Equal(SkyGridMode.None, SextantReadingState.CycleGridMode());
    }

    [Fact]
    public void Reset_Ends_Reading_And_Restores_Angle_Only_Mode()
    {
        SextantReadingState.Begin();
        SextantReadingState.CycleGridMode();

        SextantReadingState.Reset();

        Assert.False(SextantReadingState.IsReading);
        Assert.Equal(SkyGridMode.None, SextantReadingState.GridMode);
    }
}
