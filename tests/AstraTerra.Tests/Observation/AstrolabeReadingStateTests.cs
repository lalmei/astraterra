using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class AstrolabeReadingStateTests
{
    [Fact]
    public void CycleTarget_Wraps_And_Normalizes_When_Book_Changes()
    {
        AstrolabeReadingState.Reset();

        AstrolabeReadingState.CycleTarget(2);
        Assert.Equal(1, AstrolabeReadingState.SelectedTargetIndex);
        AstrolabeReadingState.CycleTarget(2);
        Assert.Equal(0, AstrolabeReadingState.SelectedTargetIndex);
        AstrolabeReadingState.CycleTarget(3);

        Assert.Equal(0, AstrolabeReadingState.NormalizeTargetIndex(1));
    }

    [Fact]
    public void Forecast_Is_Clamped_And_Cleared_When_Use_Ends()
    {
        AstrolabeReadingState.Reset();
        AstrolabeReadingState.Begin();

        AstrolabeReadingState.AdjustForecastHours(10, 100);
        AstrolabeReadingState.AdjustForecastHours(-20, 100);
        Assert.Equal(0, AstrolabeReadingState.ForecastHours);
        AstrolabeReadingState.AdjustForecastHours(150, 100);
        Assert.Equal(100, AstrolabeReadingState.ForecastHours);

        AstrolabeReadingState.End();

        Assert.False(AstrolabeReadingState.IsReading);
        Assert.Equal(0, AstrolabeReadingState.ForecastHours);
    }
}
