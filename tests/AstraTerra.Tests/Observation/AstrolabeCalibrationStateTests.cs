using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class AstrolabeCalibrationStateTests
{
    [Fact]
    public void Sighting_Fills_The_Bar_And_Clears_When_It_Ends()
    {
        AstrolabeCalibrationState.Reset();
        Assert.False(AstrolabeCalibrationState.IsCalibrating);

        AstrolabeCalibrationState.Update(AstrolabeCalibrationPolicy.CalibrationSeconds / 2f, obstacle: null);
        Assert.True(AstrolabeCalibrationState.IsCalibrating);
        Assert.Equal(0.5f, AstrolabeCalibrationState.Progress, 3);
        Assert.Null(AstrolabeCalibrationState.Obstacle);

        AstrolabeCalibrationState.End();
        Assert.False(AstrolabeCalibrationState.IsCalibrating);
        Assert.Equal(0f, AstrolabeCalibrationState.Progress);
        Assert.Null(AstrolabeCalibrationState.Obstacle);
    }

    /// <summary>
    /// While the sighting is refused the bar empties rather than freezing part-full. A bar stopped
    /// at 40% reads as the game having hung; an empty bar next to "no sky overhead" reads as the
    /// instrument waiting for the player to move.
    /// </summary>
    [Fact]
    public void A_Blocked_Sighting_Shows_An_Empty_Bar_And_The_Reason()
    {
        AstrolabeCalibrationState.Reset();

        AstrolabeCalibrationState.Update(2.5f, obstacle: "No sky overhead — step out from under cover to sight the pole.");

        Assert.True(AstrolabeCalibrationState.IsCalibrating);
        Assert.Equal(0f, AstrolabeCalibrationState.Progress);
        Assert.Contains("cover", AstrolabeCalibrationState.Obstacle);

        AstrolabeCalibrationState.Reset();
    }
}
