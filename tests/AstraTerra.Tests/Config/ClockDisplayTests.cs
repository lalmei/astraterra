using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class ClockDisplayTests
{
    [Fact]
    public void Universal_Display_Keeps_The_World_Clock()
    {
        var hours = ClockDisplay.GetDisplayedSolarTimeHours(
            totalDays: 10.5,
            hoursPerDay: 24,
            longitudeDegrees: 90,
            DisplayedClockTime.Universal);

        Assert.Equal(12.0, hours, 6);
    }

    [Fact]
    public void Local_Solar_Display_Shifts_By_Longitude()
    {
        var hours = ClockDisplay.GetDisplayedSolarTimeHours(
            totalDays: 10.5,
            hoursPerDay: 24,
            longitudeDegrees: 90,
            DisplayedClockTime.LocalSolar);

        Assert.Equal(18.0, hours, 6);
    }

    [Fact]
    public void Local_Solar_Display_Uses_The_Configured_World_Day_Length()
    {
        var hours = ClockDisplay.GetDisplayedSolarTimeHours(
            totalDays: 10.5,
            hoursPerDay: 16,
            longitudeDegrees: 90,
            DisplayedClockTime.LocalSolar);

        Assert.Equal(12.0, hours, 6);
    }

    [Fact]
    public void Time_Zones_Step_By_Whole_Hours()
    {
        var hours = ClockDisplay.GetDisplayedSolarTimeHours(
            totalDays: 10.5,
            hoursPerDay: 24,
            longitudeDegrees: 80,
            DisplayedClockTime.LocalTimeZone);

        Assert.Equal(17.0, hours, 6);
    }

    [Fact]
    public void Time_Zones_Remain_Whole_Clock_Hours_On_A_Non_24_Hour_World()
    {
        var hours = ClockDisplay.GetDisplayedSolarTimeHours(
            totalDays: 10.5,
            hoursPerDay: 16,
            longitudeDegrees: 80,
            DisplayedClockTime.LocalTimeZone);

        Assert.Equal(12.0, hours, 6);
    }

    [Fact]
    public void Config_Defaults_To_Continuous_Local_Solar_Time()
    {
        var config = new AstraTerraConfig();

        Assert.Equal(DisplayedClockTime.LocalSolar, config.GetDisplayedClockTime());
        Assert.Equal("local", config.DisplayedClockTime);
    }
}
