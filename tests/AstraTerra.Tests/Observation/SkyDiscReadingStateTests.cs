using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SkyDiscReadingStateTests
{
    [Fact]
    public void A_Lowered_Disc_Does_Not_Turn()
    {
        SkyDiscReadingState.Reset();

        SkyDiscReadingState.Turn(3);

        Assert.Equal(0f, SkyDiscReadingState.FaceTurnDeg);
    }

    [Fact]
    public void The_Wheel_Turns_The_Raised_Disc_A_Notch_At_A_Time()
    {
        SkyDiscReadingState.Reset();
        SkyDiscReadingState.Begin();

        SkyDiscReadingState.Turn(1);
        Assert.Equal(SkyDiscReadingState.TurnStepDeg, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.Turn(2);
        Assert.Equal(3f * SkyDiscReadingState.TurnStepDeg, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.Turn(-3);
        Assert.Equal(0f, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.Reset();
    }

    [Fact]
    public void Turning_Past_A_Full_Circle_Comes_Round_Rather_Than_Running_On()
    {
        SkyDiscReadingState.Reset();
        SkyDiscReadingState.Begin();

        // A disc has no stop: turned far enough either way it is back where it started.
        var notchesPerTurn = (int)(360f / SkyDiscReadingState.TurnStepDeg);
        SkyDiscReadingState.Turn(notchesPerTurn + 1);
        Assert.Equal(SkyDiscReadingState.TurnStepDeg, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.Turn(-2);
        Assert.Equal(360f - SkyDiscReadingState.TurnStepDeg, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.Reset();
    }

    [Fact]
    public void A_Disc_Raised_Again_Comes_Up_The_Way_It_Was_Made()
    {
        SkyDiscReadingState.Reset();
        SkyDiscReadingState.Begin();
        SkyDiscReadingState.Turn(4);

        // Holding it up is not a new grip; only letting it down and raising it again is.
        SkyDiscReadingState.Begin();
        Assert.Equal(4f * SkyDiscReadingState.TurnStepDeg, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.End();
        SkyDiscReadingState.Begin();
        Assert.Equal(0f, SkyDiscReadingState.FaceTurnDeg);

        SkyDiscReadingState.Reset();
    }

    [Fact]
    public void Only_The_Raised_Face_Is_Marked_As_The_Drawing_In_Hand()
    {
        SkyDiscReadingState.Reset();
        Assert.False(SkyDiscReadingState.IsDrawingRaisedFace);

        SkyDiscReadingState.BeginDrawingRaisedFace();
        Assert.True(SkyDiscReadingState.IsDrawingRaisedFace);

        SkyDiscReadingState.EndDrawingRaisedFace();
        Assert.False(SkyDiscReadingState.IsDrawingRaisedFace);
    }
}
