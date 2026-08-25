using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class AstrolabeJournalTargetsTests
{
    [Fact]
    public void A_Wanderer_Nobody_Has_Picked_Out_Is_Not_A_Target_At_All()
    {
        var targets = AstrolabeJournalTargets.KnownWanderers(
            [Planet("mars"), Planet("saturn")],
            new ObservationLog(),
            new PlanetJournal());

        Assert.Empty(targets);
    }

    [Fact]
    public void A_Wanderer_The_Observers_Own_Entries_Pinned_Down_Carries_What_Is_Behind_It()
    {
        var log = new ObservationLog();
        var first = Sight(log, 412);
        var second = Sight(log, 415);
        log.Claim(SkyClass.Wanderer, "Ember", [first.Id, second.Id], day: 416, boundId: "mars");

        var target = Assert.Single(AstrolabeJournalTargets.KnownWanderers(
            [Planet("mars"), Planet("saturn")],
            log,
            new PlanetJournal()));

        Assert.Equal("mars", target.SourceId);
        Assert.Equal(ObservationConfidence.Fair, target.Confidence);
        Assert.Contains("from 2 sightings over 3 days", target.Provenance);
    }

    [Fact]
    public void A_Wanderer_Written_Down_Under_The_Old_Mechanic_Still_Aims_But_Cannot_Say_Why()
    {
        var planets = new PlanetJournal();
        planets.Identify("saturn");

        var target = Assert.Single(AstrolabeJournalTargets.KnownWanderers(
            [Planet("mars"), Planet("saturn")],
            new ObservationLog(),
            planets));

        Assert.Equal("saturn", target.SourceId);
        Assert.Equal(ObservationConfidence.Legacy, target.Confidence);
        Assert.Contains("until re-observed", target.Provenance);
    }

    [Fact]
    public void A_Conclusion_That_Pinned_Down_Nothing_Aims_At_Nothing()
    {
        var log = new ObservationLog();
        var record = Sight(log, 412);
        log.Claim(SkyClass.Wanderer, "Ember", [record.Id], day: 413);

        Assert.Empty(AstrolabeJournalTargets.KnownWanderers([Planet("mars")], log, new PlanetJournal()));
    }

    [Fact]
    public void A_Drawn_Figure_Rests_On_The_Drawing()
    {
        var target = Assert.Single(AstrolabeJournalTargets.Drawn(
            [AstrolabeTarget.Fixed(AstrolabeTargetKind.Constellation, "1", "Lantern", 80.0, 20.0, starCount: 5)]));

        Assert.Equal("drawn from 5 stars", target.Provenance);
        Assert.Equal(ObservationConfidence.Good, target.Confidence);
    }

    [Fact]
    public void A_Comet_Says_Whose_Arithmetic_It_Is()
    {
        var target = Assert.Single(AstrolabeJournalTargets.FromAlmanac(
            [AstrolabeTarget.Fixed(AstrolabeTargetKind.Comet, "swift", "Swift", 80.0, 20.0)]));

        Assert.Contains("almanac", target.Provenance);
    }

    private static AstrolabeTarget Planet(string id)
        => AstrolabeTarget.Fixed(AstrolabeTargetKind.Planet, id, PlanetJournal.UnidentifiedDisplayName, 80.0, 20.0);

    private static ObservationRecord Sight(ObservationLog log, int day)
        => log.Record(34.2, 118.0, 1.4, day, 21.5, 51.0, InstrumentResolution.BrassSextantDeg, siderealAngleDeg: 90.0);
}
