using AstraTerra.Commands;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Commands;

public sealed class SightingReportTests
{
    [Fact]
    public void An_Empty_Ledger_Says_How_To_Start_One()
    {
        Assert.Contains("Sneak while sighting", SightingReport.Describe(new ObservationLog()));
    }

    [Fact]
    public void The_Listing_Shows_Entries_Sets_And_Any_Conclusion_Drawn()
    {
        var log = new ObservationLog();
        var record = Sight(log, 412);
        log.Claim(SkyClass.Wanderer, "Ember", [record.Id], day: 413);

        var report = SightingReport.Describe(log);

        Assert.Contains("#1 day 412", report);
        Assert.Contains("Set 1:", report);
        Assert.Contains("[Ember]", report);
    }

    [Theory]
    [InlineData("star", SkyClass.FixedStar)]
    [InlineData("Wanderer", SkyClass.Wanderer)]
    [InlineData("comet", SkyClass.Comet)]
    public void A_Class_Can_Be_Named_In_The_Words_An_Observer_Would_Use(string token, SkyClass expected)
    {
        Assert.True(SightingReport.TryParseClass(token, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void A_Class_Nobody_Recognises_Is_Refused()
    {
        Assert.False(SightingReport.TryParseClass("nebula", out _));
    }

    [Fact]
    public void A_Set_Number_Resolves_To_Every_Entry_In_It()
    {
        var log = new ObservationLog();
        Sight(log, 412);
        Sight(log, 415);

        var entries = SightingReport.ResolveEntries(log, "1", out var error);

        Assert.Equal(string.Empty, error);
        Assert.Equal([1L, 2L], entries);
    }

    [Fact]
    public void A_Single_Entry_Can_Be_Pointed_At_By_Its_Own_Number()
    {
        var log = new ObservationLog();
        Sight(log, 412);
        Sight(log, 415);

        Assert.Equal([2L], SightingReport.ResolveEntries(log, "#2", out _));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("#9")]
    [InlineData("orion")]
    public void Pointing_At_Something_That_Is_Not_There_Explains_Itself(string token)
    {
        var log = new ObservationLog();
        Sight(log, 412);

        Assert.Null(SightingReport.ResolveEntries(log, token, out var error));
        Assert.NotEqual(string.Empty, error);
    }

    private static ObservationRecord Sight(ObservationLog log, int day)
        => log.Record(34.2, 118.0, 1.4, day, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
}
