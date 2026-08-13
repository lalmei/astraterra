using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class AstrolabeServiceTests
{
    [Fact]
    public void BuildTargets_Uses_Journal_Names_And_Circular_Right_Ascension()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdge(1, 2);
        journal.Replace(record with { Name = "Meridian Seam" });
        var catalog = new StarCatalog(
            [
                new StarCatalogEntry(1, 350, 10, 1, null, true),
                new StarCatalogEntry(2, 10, 30, 2, null, true)
            ],
            []);

        var target = Assert.Single(AstrolabeService.BuildTargets(journal, catalog));

        var coordinates = target.Ephemeris.PositionAt(0);

        Assert.Equal("Meridian Seam", target.DisplayName);
        Assert.Equal(AstrolabeTargetKind.Constellation, target.Kind);
        Assert.True(coordinates.RightAscensionDeg < 0.001 || coordinates.RightAscensionDeg > 359.999);
        Assert.Equal(20, coordinates.DeclinationDeg, 6);
        Assert.Equal(2, target.StarCount);
    }

    [Fact]
    public void BuildTargets_Ignores_Constellations_With_No_Catalog_Stars()
    {
        var journal = new ConstellationJournal();
        journal.CreateFromEdge(100, 200);

        var targets = AstrolabeService.BuildTargets(journal, new StarCatalog([], []));

        Assert.Empty(targets);
    }

    [Fact]
    public void Read_Reports_Culmination_At_The_Meridian()
    {
        const double totalDays = 0;
        const int daysPerYear = 120;
        const double hoursPerDay = 24;
        var rightAscension = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays,
            daysPerYear,
            hoursPerDay);
        var target = Target("Zenith", rightAscension, 0);

        var reading = AstrolabeService.Read(
            target,
            latitudeDeg: 0,
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg: 0);

        Assert.Equal(90, reading.AltitudeDeg, 6);
        Assert.Equal(AstrolabeMotionState.Culminating, reading.MotionState);
        Assert.Equal(AstrolabeHorizonClass.Normal, reading.HorizonClass);
        Assert.Equal(0, reading.HoursUntilTransit, 6);
    }

    [Fact]
    public void Read_Uses_Live_Sky_Direction_To_Distinguish_Rising_And_Setting()
    {
        const double totalDays = 0;
        const int daysPerYear = 120;
        const double hoursPerDay = 24;
        var localSidereal = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays,
            daysPerYear,
            hoursPerDay);
        // A target whose right ascension is still ahead of local sidereal time has not transited
        // yet, so it is east of the meridian and climbing.
        var risingTarget = Target("Rising", localSidereal + 45, 0);
        var settingTarget = Target("Setting", localSidereal - 45, 0);

        var rising = AstrolabeService.Read(risingTarget, 0, totalDays, daysPerYear, hoursPerDay, 0);
        var setting = AstrolabeService.Read(settingTarget, 0, totalDays, daysPerYear, hoursPerDay, 0);

        Assert.Equal(AstrolabeMotionState.Rising, rising.MotionState);
        Assert.Equal(AstrolabeMotionState.Setting, setting.MotionState);
        Assert.InRange(rising.AzimuthDeg, 0.0, 180.0);
        Assert.InRange(setting.AzimuthDeg, 180.0, 360.0);
    }

    [Fact]
    public void Read_Counts_Down_To_The_Next_Transit()
    {
        const double totalDays = 0;
        const int daysPerYear = 120;
        const double hoursPerDay = 24;
        var localSidereal = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays,
            daysPerYear,
            hoursPerDay);

        var justBeforeTransit = AstrolabeService.Read(
            Target("Soon", localSidereal + 45, 0),
            latitudeDeg: 0,
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg: 0);
        var justAfterTransit = AstrolabeService.Read(
            Target("Missed it", localSidereal - 45, 0),
            latitudeDeg: 0,
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg: 0);

        var cycleHours = justBeforeTransit.SiderealCycleHours;

        Assert.Equal(cycleHours * 45.0 / 360.0, justBeforeTransit.HoursUntilTransit, 6);
        Assert.Equal(cycleHours * 315.0 / 360.0, justAfterTransit.HoursUntilTransit, 6);
    }

    [Fact]
    public void SiderealCycle_Runs_Slightly_Shorter_Than_The_Solar_Day()
    {
        const int daysPerYear = 120;
        const double hoursPerDay = 24;

        var reading = AstrolabeService.Read(
            Target("Any", 0, 0),
            latitudeDeg: 0,
            totalDays: 0,
            daysPerYear,
            hoursPerDay,
            longitudeDeg: 0);

        Assert.InRange(reading.SiderealCycleHours, hoursPerDay * 0.9, hoursPerDay);
    }

    [Fact]
    public void Read_Classifies_Circumpolar_And_Never_Rising_Targets()
    {
        var circumpolar = AstrolabeService.Read(
            Target("North", 0, 60),
            latitudeDeg: 60,
            totalDays: 0,
            daysPerYear: 120,
            hoursPerDay: 24,
            longitudeDeg: 0);
        var neverRises = AstrolabeService.Read(
            Target("South", 0, -60),
            latitudeDeg: 60,
            totalDays: 0,
            daysPerYear: 120,
            hoursPerDay: 24,
            longitudeDeg: 0);

        Assert.Equal(AstrolabeHorizonClass.Circumpolar, circumpolar.HorizonClass);
        Assert.Equal(AstrolabeHorizonClass.NeverRises, neverRises.HorizonClass);
        Assert.Equal(AstrolabeMotionState.NeverRises, neverRises.MotionState);
    }

    [Theory]
    [InlineData(0, "N")]
    [InlineData(44, "NE")]
    [InlineData(90, "E")]
    [InlineData(181, "S")]
    [InlineData(270, "W")]
    [InlineData(359, "N")]
    public void CompassPoint_Formats_Eight_Way_Directions(double azimuthDeg, string expected)
    {
        Assert.Equal(expected, AstrolabeService.CompassPoint(azimuthDeg));
    }

    [Fact]
    public void BuildPlanetTargets_Lists_Every_Planet_Whether_Or_Not_It_Is_Up()
    {
        var targets = AstrolabeService.BuildPlanetTargets(LoadPlanets(), daysPerYear: 108, hoursPerDay: 24);

        Assert.Equal(5, targets.Count);
        Assert.All(targets, target => Assert.Equal(AstrolabeTargetKind.Planet, target.Kind));
        Assert.All(targets, target => Assert.Equal(0, target.StarCount));
        Assert.Contains(targets, target => target.SourceId == "mars");

        // The catalog's own names must not leak through the target: a wanderer is nameless until an
        // observer writes one down, and the name comes from their book.
        Assert.All(targets, target => Assert.Equal(PlanetJournal.UnidentifiedDisplayName, target.DisplayName));
        Assert.DoesNotContain(targets, target => target.DisplayName == "Mars");
    }

    [Fact]
    public void A_Planet_Is_Read_Where_It_Will_Be_At_The_Forecast_Time()
    {
        var mars = AstrolabeService.BuildPlanetTargets(LoadPlanets(), daysPerYear: 108, hoursPerDay: 24)
            .Single(target => target.SourceId == "mars");

        var now = AstrolabeService.Read(mars, latitudeDeg: 45, totalDays: 0, daysPerYear: 108, hoursPerDay: 24, longitudeDeg: 0);
        var inHalfAYear = AstrolabeService.Read(mars, latitudeDeg: 45, totalDays: 54, daysPerYear: 108, hoursPerDay: 24, longitudeDeg: 0);

        // A fixed target would report the same right ascension at both times; the point of an
        // ephemeris on the target is that a wanderer does not.
        Assert.True(
            Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(
                now.Coordinates.RightAscensionDeg,
                inHalfAYear.Coordinates.RightAscensionDeg)) > 10.0,
            "Mars did not move between the two readings.");
    }

    [Fact]
    public void A_Constellation_Reads_The_Same_Position_At_Any_Time()
    {
        var target = Target("Fixed", rightAscensionDeg: 120, declinationDeg: 20);

        var now = AstrolabeService.Read(target, latitudeDeg: 45, totalDays: 0, daysPerYear: 108, hoursPerDay: 24, longitudeDeg: 0);
        var later = AstrolabeService.Read(target, latitudeDeg: 45, totalDays: 54, daysPerYear: 108, hoursPerDay: 24, longitudeDeg: 0);

        Assert.Equal(now.Coordinates, later.Coordinates);
    }

    [Fact]
    public void BuildPlanetTargets_Rejects_A_World_With_No_Year_Or_No_Day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AstrolabeService.BuildPlanetTargets(LoadPlanets(), daysPerYear: 0, hoursPerDay: 24));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AstrolabeService.BuildPlanetTargets(LoadPlanets(), daysPerYear: 108, hoursPerDay: 0));
    }

    private static PlanetCatalog LoadPlanets()
        => PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));

    private static AstrolabeTarget Target(string displayName, double rightAscensionDeg, double declinationDeg)
        => AstrolabeTarget.Fixed(
            AstrolabeTargetKind.Constellation,
            displayName,
            displayName,
            rightAscensionDeg,
            declinationDeg,
            starCount: 2);
}
