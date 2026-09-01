using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class SkyClockTests
{
    private const double HoursPerDay = 24.0;
    private const double PeakAltitudeDeg = 60.0;

    [Fact]
    public void Reports_The_Hour_Of_The_World_Day()
    {
        var reading = ReadAtHour(21.5);

        Assert.Equal(21.5, reading.LocalTimeHours, 6);
    }

    [Fact]
    public void Reports_Local_Solar_Time_Without_Changing_The_Timestamp_Used_For_The_Sun()
    {
        double? firstSampledTotalDays = null;

        var reading = SkyClock.Read(
            totalDays: 10.5,
            hoursPerDay: HoursPerDay,
            days =>
            {
                firstSampledTotalDays ??= days;
                return 45.0;
            },
            longitudeDegrees: 90.0);

        Assert.Equal(18.0, reading.LocalTimeHours, 6);
        Assert.Equal(10.5, firstSampledTotalDays!.Value, 6);
    }

    [Fact]
    public void Midnight_Is_Night_And_Counts_Down_To_Sunrise()
    {
        var reading = ReadAtHour(0);

        Assert.Equal(SkyPhase.Night, reading.Phase);
        Assert.Equal(-PeakAltitudeDeg, reading.SunAltitudeDeg, 6);
        Assert.NotNull(reading.HoursUntilSunrise);
        Assert.Equal(6.0, reading.HoursUntilSunrise!.Value, 3);
        Assert.Equal(18.0, reading.HoursUntilSunset!.Value, 3);
    }

    [Fact]
    public void Noon_Is_Daylight_And_Counts_Down_To_Sunset()
    {
        var reading = ReadAtHour(12);

        Assert.Equal(SkyPhase.Day, reading.Phase);
        Assert.Equal(PeakAltitudeDeg, reading.SunAltitudeDeg, 6);
        Assert.Equal(6.0, reading.HoursUntilSunset!.Value, 3);
    }

    [Theory]
    [InlineData(5.8, SkyPhase.Dawn)]
    [InlineData(18.2, SkyPhase.Dusk)]
    public void Twilight_Splits_Into_Dawn_And_Dusk_By_Which_Way_The_Sun_Moves(double hour, SkyPhase expected)
    {
        var reading = ReadAtHour(hour);

        Assert.InRange(reading.SunAltitudeDeg, SkyClock.CivilTwilightAltitudeDeg, 0.0);
        Assert.Equal(expected, reading.Phase);
    }

    [Fact]
    public void Polar_Day_Reports_No_Sunset()
    {
        var reading = SkyClock.Read(totalDays: 10, HoursPerDay, _ => 12.0);

        Assert.Equal(SkyPhase.Day, reading.Phase);
        Assert.Null(reading.HoursUntilSunset);
        Assert.Null(reading.HoursUntilSunrise);
    }

    [Fact]
    public void Polar_Night_Reports_No_Sunrise()
    {
        var reading = SkyClock.Read(totalDays: 10, HoursPerDay, _ => -20.0);

        Assert.Equal(SkyPhase.Night, reading.Phase);
        Assert.Null(reading.HoursUntilSunrise);
        Assert.Null(reading.HoursUntilSunset);
    }

    [Fact]
    public void Finds_A_Brief_Daylight_Window_Between_Coarse_Samples()
    {
        var reading = SkyClock.Read(totalDays: 10, HoursPerDay, BriefDaylightAltitudeDeg);

        Assert.NotNull(reading.HoursUntilSunrise);
        Assert.Equal(12.0 + (2.5 / 60.0), reading.HoursUntilSunrise!.Value, 3);
        Assert.NotNull(reading.HoursUntilSunset);
        Assert.Equal(12.0 + (12.5 / 60.0), reading.HoursUntilSunset!.Value, 3);
    }

    [Fact]
    public void Rejects_A_Zero_Length_Day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SkyClock.Read(totalDays: 0, hoursPerDay: 0, _ => 0.0));
    }

    private static SkyClockReading ReadAtHour(double hour)
        => SkyClock.Read(10 + (hour / HoursPerDay), HoursPerDay, SyntheticSunAltitudeDeg);

    /// <summary>A sun that rises at 06:00, peaks at noon and sets at 18:00.</summary>
    private static double SyntheticSunAltitudeDeg(double totalDays)
    {
        var hourOfDay = CelestialMath.GetUniversalSolarTimeHours(totalDays, HoursPerDay);
        return PeakAltitudeDeg * Math.Sin(2.0 * Math.PI * (hourOfDay - 6.0) / HoursPerDay);
    }

    /// <summary>
    /// A near-polar sun above the horizon for ten minutes, entirely between the 12:00 and 12:15
    /// samples used by the coarse horizon search.
    /// </summary>
    private static double BriefDaylightAltitudeDeg(double totalDays)
    {
        const double peakHour = 12.0 + (7.5 / 60.0);
        const double halfWindowHours = 5.0 / 60.0;
        var hourOfDay = CelestialMath.GetUniversalSolarTimeHours(totalDays, HoursPerDay);
        var offset = (hourOfDay - peakHour) / halfWindowHours;
        return 1.0 - (offset * offset);
    }
}
