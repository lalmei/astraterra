using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SightingClaimTests
{
    [Fact]
    public void A_Conclusion_Names_The_Entries_It_Rests_On()
    {
        var log = new ObservationLog();
        var first = Sight(log, 412);
        var second = Sight(log, 415);

        var claim = log.Claim(SkyClass.Wanderer, "Ember", [first.Id, second.Id], day: 416);

        Assert.Equal("Ember", claim.DisplayName);
        Assert.Equal([first.Id, second.Id], claim.RecordIds);
        Assert.Equal("from 2 sightings", claim.Provenance);
        Assert.Equal(claim, log.FindClaimFor(second.Id));
    }

    [Fact]
    public void Changing_Your_Mind_Replaces_The_Earlier_Reading_And_Keeps_The_Evidence()
    {
        var log = new ObservationLog();
        var first = Sight(log, 412);
        var second = Sight(log, 415);
        log.Claim(SkyClass.FixedStar, "Anchor", [first.Id, second.Id], day: 416);

        log.Claim(SkyClass.Wanderer, "Ember", [second.Id], day: 480);

        var claim = Assert.Single(log.Claims);
        Assert.Equal(SkyClass.Wanderer, claim.Class);
        Assert.Equal(2, log.Observations.Count);
    }

    [Fact]
    public void An_Unnamed_Conclusion_Still_Reads_As_Something()
    {
        var log = new ObservationLog();
        var record = Sight(log, 412);

        var claim = log.Claim(SkyClass.Comet, name: null, [record.Id], day: 413);

        Assert.Equal("Unnamed comet", claim.DisplayName);
    }

    [Fact]
    public void A_Conclusion_About_Nothing_Is_Refused()
    {
        var log = new ObservationLog();
        Sight(log, 412);

        Assert.Throws<InvalidOperationException>(() => log.Claim(SkyClass.FixedStar, "Ghost", [99], day: 413));
    }

    [Fact]
    public void Conclusions_Round_Trip_And_Drop_Any_Whose_Evidence_Is_Gone()
    {
        var log = new ObservationLog();
        var first = Sight(log, 412);
        var second = Sight(log, 415);
        log.Claim(SkyClass.FixedStar, "Anchor", [first.Id], day: 416);
        log.Claim(SkyClass.Wanderer, "Ember", [second.Id], day: 416);
        log.Remove(second.Id);

        var loaded = ObservationLogPersistence.Deserialize(ObservationLogPersistence.Serialize(log));

        var claim = Assert.Single(loaded.Claims);
        Assert.Equal("Anchor", claim.Name);
        Assert.Equal(3, loaded.Claim(SkyClass.Comet, "Later", [first.Id], day: 500).Id);
    }

    private static ObservationRecord Sight(ObservationLog log, int day)
        => log.Record(34.2, 118.0, 1.4, day, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
}
