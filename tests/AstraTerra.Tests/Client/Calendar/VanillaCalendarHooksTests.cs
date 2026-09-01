using AstraTerra.Client.Calendar;
using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Client.Calendar;

public sealed class VanillaCalendarHooksTests
{
    private const string PrettyDate = "14. June, Year 3, 21:40";

    [Fact]
    public void Vanilla_Reckoning_Passes_Through_Untouched()
    {
        Assert.Equal(PrettyDate, VanillaCalendarHooks.Redact(CalendarDisplay.Full, PrettyDate, 21.67f));
    }

    [Fact]
    public void Dropping_The_Date_Keeps_The_Hour_The_Sun_Gives_Away_Anyway()
    {
        Assert.Equal("unreckoned, 21:40", VanillaCalendarHooks.Redact(CalendarDisplay.Clock, PrettyDate, 21.67f));
    }

    [Fact]
    public void The_Hour_Is_Padded_So_It_Reads_As_A_Time()
    {
        Assert.Equal("unreckoned, 06:05", VanillaCalendarHooks.Redact(CalendarDisplay.Clock, PrettyDate, 6.09f));
    }

    [Fact]
    public void Hiding_Everything_Says_So_Rather_Than_Reporting_A_Wrong_Date()
    {
        var redacted = VanillaCalendarHooks.Redact(CalendarDisplay.None, PrettyDate, 21.67f);

        Assert.Equal("unreckoned", redacted);
        Assert.DoesNotContain("Year", redacted);
    }

    [Fact]
    public void Displayed_Clock_Parts_Preserve_The_Game_Clock_Formatting()
    {
        VanillaCalendarHooks.Reset();

        Assert.Equal((18, 30), VanillaCalendarHooks.GetDisplayedClockParts(displayedHour: 18.5f));
    }

    [Fact]
    public void Nothing_Is_Redacted_Before_A_Client_Has_Been_Wired_Up()
    {
        VanillaCalendarHooks.Reset();

        Assert.Equal(CalendarDisplay.Full, VanillaCalendarHooks.Display);
        Assert.False(VanillaCalendarHooks.IsPlayerCalendar(new object()));
        Assert.False(VanillaCalendarHooks.IsPlayerCalendar(null));
    }
}
