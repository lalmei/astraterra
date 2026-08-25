using AstraTerra.Astronomy;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SightingComparisonTests
{
    private const double LatitudeDeg = 51.0;

    [Fact]
    public void A_Star_Sighted_On_Two_Nights_Reads_As_One_Set_That_Held_Its_Place()
    {
        var log = new ObservationLog();
        Sight(log, rightAscensionDeg: 80.0, declinationDeg: 20.0, day: 412, siderealAngleDeg: 90.0);
        Sight(log, rightAscensionDeg: 80.0, declinationDeg: 20.0, day: 415, siderealAngleDeg: 210.0);

        var group = Assert.Single(SightingComparison.Group(log.Observations));

        Assert.Equal(2, group.Records.Count);
        Assert.False(group.MovedBeyondResolution);
        Assert.Contains("held its place", group.Describe());
    }

    [Fact]
    public void A_Body_That_Wandered_Reports_How_Far_And_How_Fast()
    {
        var log = new ObservationLog();
        Sight(log, rightAscensionDeg: 80.0, declinationDeg: 0.0, day: 412, siderealAngleDeg: 90.0);
        Sight(log, rightAscensionDeg: 82.0, declinationDeg: 0.0, day: 416, siderealAngleDeg: 250.0);

        var group = Assert.Single(SightingComparison.Group(log.Observations));

        Assert.True(group.MovedBeyondResolution);
        Assert.Equal(2.0, group.SeparationDeg, 1);
        Assert.Equal(0.5, group.DegreesPerDay, 2);
        Assert.Contains("°/day", group.Describe());
    }

    [Fact]
    public void Two_Bodies_Far_Apart_Stay_Two_Sets()
    {
        var log = new ObservationLog();
        Sight(log, rightAscensionDeg: 80.0, declinationDeg: 20.0, day: 412, siderealAngleDeg: 90.0);
        Sight(log, rightAscensionDeg: 200.0, declinationDeg: -10.0, day: 412, siderealAngleDeg: 90.0);

        var groups = SightingComparison.Group(log.Observations);

        Assert.Equal(2, groups.Count);
        Assert.Equal([1, 2], groups.Select(group => group.Number));
    }

    [Fact]
    public void An_Entry_Joins_The_Set_It_Landed_Nearest_To()
    {
        var log = new ObservationLog();
        var first = Sight(log, rightAscensionDeg: 80.0, declinationDeg: 0.0, day: 412, siderealAngleDeg: 90.0);
        var second = Sight(log, rightAscensionDeg: 84.0, declinationDeg: 0.0, day: 412, siderealAngleDeg: 90.0);
        var later = Sight(log, rightAscensionDeg: 84.2, declinationDeg: 0.0, day: 413, siderealAngleDeg: 130.0);

        var groups = SightingComparison.Group(log.Observations);

        Assert.Equal(2, groups.Count);
        Assert.Equal([first.Id], groups[0].Records.Select(record => record.Id));
        Assert.Equal([second.Id, later.Id], groups[1].Records.Select(record => record.Id));
    }

    [Fact]
    public void An_Entry_With_No_Sky_Angle_Stands_Alone_And_Says_So()
    {
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, LatitudeDeg, InstrumentResolution.BrassSextantDeg);
        log.Record(34.2, 118.0, 1.4, 413, 21.5, LatitudeDeg, InstrumentResolution.BrassSextantDeg);

        var groups = SightingComparison.Group(log.Observations);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.Contains("nothing yet to compare", group.Describe()));
    }

    [Fact]
    public void A_Crude_Instrument_Cannot_Tell_A_Small_Movement_From_None()
    {
        var log = new ObservationLog();
        Sight(log, 80.0, 0.0, 412, 90.0, InstrumentResolution.CrossStaffDeg);
        Sight(log, 80.5, 0.0, 413, 130.0, InstrumentResolution.CrossStaffDeg);

        var group = Assert.Single(SightingComparison.Group(log.Observations));

        Assert.False(group.MovedBeyondResolution);
        Assert.Contains("held its place", group.Describe());
    }

    private static ObservationRecord Sight(
        ObservationLog log,
        double rightAscensionDeg,
        double declinationDeg,
        int day,
        double siderealAngleDeg,
        double resolutionDeg = InstrumentResolution.BrassSextantDeg)
    {
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            rightAscensionDeg,
            declinationDeg,
            LatitudeDeg,
            siderealAngleDeg);

        return log.Record(
            horizontal.AltitudeDeg,
            horizontal.AzimuthDeg,
            visualMagnitude: 1.4,
            day: day,
            hour: 21.5,
            latitudeDeg: LatitudeDeg,
            resolutionDeg: resolutionDeg,
            siderealAngleDeg: siderealAngleDeg);
    }
}
