using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class CometApparitionScheduleTests
{
    private static CometEntry Comet(
        double periodYears = 10.0,
        double firstPerihelionYear = 2.0,
        double windowHalfWidthYears = 0.1,
        double peakMagnitude = 0.0,
        double edgeMagnitude = 8.0,
        double brighteningExponent = 0.5,
        double peakTailLengthDeg = 20.0)
        => new(
            "test",
            "Test",
            periodYears,
            firstPerihelionYear,
            windowHalfWidthYears,
            peakMagnitude,
            edgeMagnitude,
            brighteningExponent,
            peakTailLengthDeg,
            [new CometPathKeyframe(-1.0, 10.0, 0.0), new CometPathKeyframe(1.0, 20.0, 0.0)],
            1f,
            1f,
            1f);

    /// <summary>
    /// A comet is authored in world years so it keeps its period whatever a world's day count is.
    /// The same passage has to fall at the same world year on a 12-day world and a 360-day one.
    /// </summary>
    [Theory]
    [InlineData(12)]
    [InlineData(108)]
    [InlineData(360)]
    public void An_Apparition_Falls_At_The_Same_World_Year_On_Any_World(int daysPerYear)
    {
        var comet = Comet(periodYears: 10.0, firstPerihelionYear: 2.0);

        var atPerihelion = CometApparitionSchedule.Read(comet, 12.0 * daysPerYear, daysPerYear);

        Assert.Equal(0.0, atPerihelion.Phase, 6);
        Assert.Equal(12.0, atPerihelion.PerihelionWorldYear, 6);
        Assert.True(atPerihelion.IsVisible);
    }

    [Fact]
    public void Passages_Repeat_On_The_Period_Forever()
    {
        var comet = Comet(periodYears: 10.0, firstPerihelionYear: 2.0);

        Assert.Equal(2.0, CometApparitionSchedule.NearestPerihelionYear(comet, 3.0), 6);
        Assert.Equal(12.0, CometApparitionSchedule.NearestPerihelionYear(comet, 9.0), 6);
        Assert.Equal(102.0, CometApparitionSchedule.NearestPerihelionYear(comet, 100.0), 6);
    }

    [Fact]
    public void The_Window_Is_The_Only_Time_It_Is_Here()
    {
        var comet = Comet(firstPerihelionYear: 2.0, windowHalfWidthYears: 0.1);

        Assert.True(CometApparitionSchedule.Read(comet, 2.0).IsVisible);
        Assert.True(CometApparitionSchedule.Read(comet, 2.09).IsVisible);
        Assert.True(CometApparitionSchedule.Read(comet, 1.91).IsVisible);
        Assert.False(CometApparitionSchedule.Read(comet, 2.2).IsVisible);
        Assert.False(CometApparitionSchedule.Read(comet, 5.0).IsVisible);
    }

    [Fact]
    public void Phase_Runs_Negative_Before_Perihelion_And_Positive_After()
    {
        var comet = Comet(firstPerihelionYear: 2.0, windowHalfWidthYears: 0.1);

        Assert.Equal(-0.5, CometApparitionSchedule.Read(comet, 1.95).Phase, 6);
        Assert.Equal(0.5, CometApparitionSchedule.Read(comet, 2.05).Phase, 6);
    }

    /// <summary>
    /// Brightness and tail length are read off one curve, so a comet cannot be at its brightest with
    /// no tail, or trailing its longest while barely visible.
    /// </summary>
    [Fact]
    public void Brightness_And_Tail_Peak_Together_And_Vanish_Together()
    {
        var comet = Comet(peakMagnitude: -1.0, edgeMagnitude: 8.0, peakTailLengthDeg: 20.0);

        Assert.Equal(1.0, CometApparitionSchedule.GetCloseness(comet, 0.0), 6);
        Assert.Equal(-1.0, CometApparitionSchedule.GetMagnitude(comet, CometApparitionSchedule.GetCloseness(comet, 0.0)), 6);
        Assert.Equal(20.0, CometApparitionSchedule.GetTailLengthDeg(comet, CometApparitionSchedule.GetCloseness(comet, 0.0)), 6);

        Assert.Equal(0.0, CometApparitionSchedule.GetCloseness(comet, 1.0), 6);
        Assert.Equal(8.0, CometApparitionSchedule.GetMagnitude(comet, CometApparitionSchedule.GetCloseness(comet, 1.0)), 6);
        Assert.Equal(0.0, CometApparitionSchedule.GetTailLengthDeg(comet, CometApparitionSchedule.GetCloseness(comet, 1.0)), 6);
    }

    [Fact]
    public void Closeness_Rises_Monotonically_Towards_Perihelion_From_Both_Sides()
    {
        var comet = Comet(brighteningExponent: 0.5);

        var approaching = new[] { -1.0, -0.75, -0.5, -0.25, 0.0 }
            .Select(phase => CometApparitionSchedule.GetCloseness(comet, phase))
            .ToArray();
        var leaving = new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }
            .Select(phase => CometApparitionSchedule.GetCloseness(comet, phase))
            .ToArray();

        for (var index = 1; index < approaching.Length; index++)
        {
            Assert.True(approaching[index] > approaching[index - 1], "closeness rises on approach");
            Assert.True(leaving[index] < leaving[index - 1], "closeness falls on the way out");
        }

        // Symmetric about perihelion: the same distance either side shows the same.
        Assert.Equal(
            CometApparitionSchedule.GetCloseness(comet, -0.4),
            CometApparitionSchedule.GetCloseness(comet, 0.4),
            9);
    }

    /// <summary>
    /// An exponent below one is what makes an apparition an event rather than a slow swell: the
    /// comet stays faint across most of its window and lifts sharply near perihelion.
    /// </summary>
    [Fact]
    public void A_Low_Exponent_Holds_The_Brightening_Back_Until_Perihelion()
    {
        var sharp = CometApparitionSchedule.GetCloseness(Comet(brighteningExponent: 0.5), 0.5);
        var even = CometApparitionSchedule.GetCloseness(Comet(brighteningExponent: 1.0), 0.5);

        Assert.True(sharp < even, "a low exponent is dimmer than an even ramp at the same phase");
        Assert.Equal(0.5, even, 6);
    }

    [Fact]
    public void A_Comet_That_Is_Up_Is_Zero_Days_Away()
    {
        var comet = Comet(firstPerihelionYear: 2.0, windowHalfWidthYears: 0.1);

        Assert.Equal(0.0, CometApparitionSchedule.DaysUntilVisible(comet, 2.0 * 100, daysPerYear: 100));
    }

    [Fact]
    public void An_Absent_Comet_Counts_Down_To_Its_Next_Window()
    {
        const int daysPerYear = 100;
        var comet = Comet(periodYears: 10.0, firstPerihelionYear: 2.0, windowHalfWidthYears: 0.1);

        // World year 5: the next window opens at 11.9, which is 690 days out.
        Assert.Equal(690.0, CometApparitionSchedule.DaysUntilVisible(comet, 5.0 * daysPerYear, daysPerYear), 6);
    }

    /// <summary>
    /// The moment a window closes is where a naive division lands a passage short and reports a
    /// return in the past.
    /// </summary>
    [Fact]
    public void The_Countdown_Never_Points_Backwards()
    {
        const int daysPerYear = 100;
        var comet = Comet(periodYears: 10.0, firstPerihelionYear: 2.0, windowHalfWidthYears: 0.1);

        foreach (var worldYears in new[] { 2.1, 2.100001, 2.1001, 11.899, 11.9, 12.1, 12.100001 })
        {
            var days = CometApparitionSchedule.DaysUntilVisible(comet, worldYears * daysPerYear, daysPerYear);
            Assert.True(days >= 0.0, $"countdown at world year {worldYears} was {days}");

            if (days > 0)
            {
                var arrival = (worldYears * daysPerYear) + days;
                Assert.True(
                    CometApparitionSchedule.Read(comet, arrival + 0.001, daysPerYear).IsVisible,
                    $"the comet was not actually up {days} days after world year {worldYears}");
            }
        }
    }
}
