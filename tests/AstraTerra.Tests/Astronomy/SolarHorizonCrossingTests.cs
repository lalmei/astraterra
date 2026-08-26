using AstraTerra.Astronomy;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class SolarHorizonCrossingTests
{
    private const double HoursPerDay = 24.0;

    [Fact]
    public void The_Mark_Is_The_Crossing_Itself_Not_The_Moment_Of_The_Click()
    {
        var atSunset = 10.0 + (18.0 / HoursPerDay);

        var early = SolarHorizonCrossing.FindNearest(atSunset - (0.75 / HoursPerDay), HoursPerDay, Sun);
        var late = SolarHorizonCrossing.FindNearest(atSunset + (0.75 / HoursPerDay), HoursPerDay, Sun);

        Assert.NotNull(early);
        Assert.NotNull(late);
        Assert.Equal(early!.AzimuthDeg, late!.AzimuthDeg, 3);
        Assert.Equal(SolarEvent.Sunset, early.Event);
    }

    [Fact]
    public void Dawn_And_Dusk_Are_Told_Apart_By_Which_Way_The_Sun_Was_Going()
    {
        var sunrise = SolarHorizonCrossing.FindNearest(10.0 + (6.0 / HoursPerDay), HoursPerDay, Sun);
        var sunset = SolarHorizonCrossing.FindNearest(10.0 + (18.0 / HoursPerDay), HoursPerDay, Sun);

        Assert.Equal(SolarEvent.Sunrise, sunrise!.Event);
        Assert.Equal(SolarEvent.Sunset, sunset!.Event);
    }

    [Fact]
    public void There_Is_Nothing_To_Mark_At_Noon()
    {
        Assert.Null(SolarHorizonCrossing.FindNearest(10.0 + (12.0 / HoursPerDay), HoursPerDay, Sun));
    }

    [Fact]
    public void A_Sun_That_Never_Sets_Offers_No_Crossing()
    {
        Assert.Null(SolarHorizonCrossing.FindNearest(
            10.5,
            HoursPerDay,
            _ => (AltitudeDeg: 12.0, AzimuthDeg: 180.0)));
    }

    /// <summary>A sun that rises at 06:00 and sets at 18:00, sweeping the horizon as it goes.</summary>
    private static (double AltitudeDeg, double AzimuthDeg) Sun(double totalDays)
    {
        var hour = (totalDays - Math.Floor(totalDays)) * HoursPerDay;
        var altitude = 50.0 * Math.Sin(Math.PI * (hour - 6.0) / 12.0);
        return (altitude, 90.0 + (15.0 * (hour - 6.0)));
    }
}
