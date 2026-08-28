using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class CalendarDisplayTests
{
    [Theory]
    [InlineData("full", CalendarDisplay.Full)]
    [InlineData("VANILLA", CalendarDisplay.Full)]
    [InlineData(" clock ", CalendarDisplay.Clock)]
    [InlineData("hour", CalendarDisplay.Clock)]
    [InlineData("none", CalendarDisplay.None)]
    [InlineData("off", CalendarDisplay.None)]
    public void TryParse_Accepts_Supported_Calendar_Values(string value, CalendarDisplay expected)
    {
        var parsed = CalendarDisplayParser.TryParse(value, out var display);

        Assert.True(parsed);
        Assert.Equal(expected, display);
    }

    [Fact]
    public void ParseOrDefault_Leaves_Vanilla_Alone_When_The_Value_Is_Unknown()
    {
        Assert.False(CalendarDisplayParser.TryParse("sundial", out _));
        Assert.Equal(CalendarDisplay.Full, CalendarDisplayParser.ParseOrDefault("sundial"));
    }

    [Fact]
    public void A_Fresh_Config_Shows_The_Whole_Vanilla_Calendar()
    {
        Assert.Equal(CalendarDisplay.Full, new AstraTerraConfig().GetCalendarDisplay());
    }

    [Theory]
    [InlineData(CalendarDisplay.Full, true, true)]
    [InlineData(CalendarDisplay.Clock, false, true)]
    [InlineData(CalendarDisplay.None, false, false)]
    public void Each_Mode_Keeps_The_Parts_It_Names(CalendarDisplay display, bool date, bool hour)
    {
        Assert.Equal(date, CalendarDisplayParser.ShowsDate(display));
        Assert.Equal(hour, CalendarDisplayParser.ShowsHour(display));
    }

    [Theory]
    [InlineData(CalendarDisplay.Full, "full")]
    [InlineData(CalendarDisplay.Clock, "clock")]
    [InlineData(CalendarDisplay.None, "none")]
    public void ToConfigValue_Round_Trips(CalendarDisplay display, string expected)
    {
        Assert.Equal(expected, CalendarDisplayParser.ToConfigValue(display));
        Assert.Equal(display, CalendarDisplayParser.ParseOrDefault(expected));
    }
}
