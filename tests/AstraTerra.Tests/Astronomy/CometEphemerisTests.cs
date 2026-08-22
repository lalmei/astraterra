using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class CometEphemerisTests
{
    private const int DaysPerYear = 100;

    private static CometEntry Comet(params CometPathKeyframe[] path)
        => new(
            "test",
            "Test",
            PeriodYears: 10.0,
            FirstPerihelionYear: 2.0,
            WindowHalfWidthYears: 0.1,
            PeakMagnitude: -1.0,
            EdgeMagnitude: 8.0,
            BrighteningExponent: 0.5,
            PeakTailLengthDeg: 20.0,
            Path: path,
            TintR: 1f,
            TintG: 1f,
            TintB: 1f);

    [Fact]
    public void The_Track_Passes_Through_Its_Keyframes()
    {
        var ephemeris = new CometEphemeris(
            Comet(
                new CometPathKeyframe(-1.0, 30.0, -10.0),
                new CometPathKeyframe(0.0, 60.0, 20.0),
                new CometPathKeyframe(1.0, 90.0, 40.0)),
            DaysPerYear);

        var atPerihelion = ephemeris.PositionAtPhase(0.0);

        Assert.Equal(60.0, atPerihelion.RightAscensionDeg, 6);
        Assert.Equal(20.0, atPerihelion.DeclinationDeg, 6);
    }

    /// <summary>
    /// Right ascension wraps at 360, so a comet authored across 0h would be dragged the long way
    /// round the sky by a plain numeric blend — 350 degrees in an evening, in the wrong direction.
    /// Rotating between the two directions takes the short way by construction.
    /// </summary>
    [Fact]
    public void A_Track_Across_Zero_Hours_Takes_The_Short_Way()
    {
        var ephemeris = new CometEphemeris(
            Comet(
                new CometPathKeyframe(-1.0, 355.0, 0.0),
                new CometPathKeyframe(1.0, 5.0, 0.0)),
            DaysPerYear);

        var halfway = ephemeris.PositionAtPhase(0.0);

        // Zero, not 180: the short way across the wrap, not straight through the far side.
        Assert.Equal(0.0, CelestialMath.NormalizeDegrees(halfway.RightAscensionDeg + 180.0) - 180.0, 4);
        Assert.Equal(0.0, halfway.DeclinationDeg, 4);
    }

    [Fact]
    public void The_Track_Moves_Steadily_Between_Keyframes()
    {
        var ephemeris = new CometEphemeris(
            Comet(
                new CometPathKeyframe(-1.0, 0.0, 0.0),
                new CometPathKeyframe(1.0, 40.0, 0.0)),
            DaysPerYear);

        Assert.Equal(10.0, ephemeris.PositionAtPhase(-0.5).RightAscensionDeg, 4);
        Assert.Equal(20.0, ephemeris.PositionAtPhase(0.0).RightAscensionDeg, 4);
        Assert.Equal(30.0, ephemeris.PositionAtPhase(0.5).RightAscensionDeg, 4);
    }

    /// <summary>
    /// The authored track only covers the apparition. Extrapolating it would answer "where is the
    /// comet in eight years' time" with somewhere it never goes; holding the end keyframes at least
    /// answers with somewhere it has been.
    /// </summary>
    [Fact]
    public void Outside_The_Window_The_Track_Holds_Its_Ends()
    {
        var ephemeris = new CometEphemeris(
            Comet(
                new CometPathKeyframe(-1.0, 10.0, -5.0),
                new CometPathKeyframe(1.0, 50.0, 25.0)),
            DaysPerYear);

        Assert.Equal(10.0, ephemeris.PositionAtPhase(-40.0).RightAscensionDeg, 4);
        Assert.Equal(50.0, ephemeris.PositionAtPhase(40.0).RightAscensionDeg, 4);
    }

    [Fact]
    public void Keyframes_Do_Not_Have_To_Be_Authored_In_Order()
    {
        var ephemeris = new CometEphemeris(
            Comet(
                new CometPathKeyframe(1.0, 40.0, 0.0),
                new CometPathKeyframe(-1.0, 0.0, 0.0),
                new CometPathKeyframe(0.0, 20.0, 0.0)),
            DaysPerYear);

        Assert.Equal(0.0, ephemeris.PositionAtPhase(-1.0).RightAscensionDeg, 4);
        Assert.Equal(40.0, ephemeris.PositionAtPhase(1.0).RightAscensionDeg, 4);
    }

    [Fact]
    public void A_Single_Keyframe_Is_A_Comet_That_Does_Not_Move()
    {
        var ephemeris = new CometEphemeris(Comet(new CometPathKeyframe(0.0, 123.0, -45.0)), DaysPerYear);

        foreach (var phase in new[] { -1.0, -0.3, 0.0, 0.7, 1.0 })
        {
            Assert.Equal(123.0, ephemeris.PositionAtPhase(phase).RightAscensionDeg, 4);
            Assert.Equal(-45.0, ephemeris.PositionAtPhase(phase).DeclinationDeg, 4);
        }
    }

    [Fact]
    public void A_Comet_With_No_Track_Is_Rejected_Rather_Than_Drawn_Somewhere_Arbitrary()
    {
        Assert.Throws<ArgumentException>(() => new CometEphemeris(Comet(), DaysPerYear));
    }

    [Fact]
    public void Magnitude_And_Tail_Follow_The_World_Clock()
    {
        var comet = Comet(new CometPathKeyframe(0.0, 0.0, 0.0));
        var ephemeris = new CometEphemeris(comet, DaysPerYear);

        // World year 2.0 is perihelion; 2.1 is the very edge of the window.
        Assert.Equal(-1.0, ephemeris.MagnitudeAt(2.0 * DaysPerYear), 6);
        Assert.Equal(20.0, ephemeris.TailLengthDegAt(2.0 * DaysPerYear), 6);

        Assert.Equal(8.0, ephemeris.MagnitudeAt(2.1 * DaysPerYear), 6);
        Assert.Equal(0.0, ephemeris.TailLengthDegAt(2.1 * DaysPerYear), 6);

        // Away entirely: nothing to draw, whatever the magnitude curve would say.
        Assert.Equal(0.0, ephemeris.TailLengthDegAt(5.0 * DaysPerYear), 6);
        Assert.False(ephemeris.ApparitionAt(5.0 * DaysPerYear).IsVisible);
    }
}
