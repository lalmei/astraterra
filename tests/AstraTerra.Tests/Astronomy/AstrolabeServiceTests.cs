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

        Assert.Equal("Meridian Seam", target.DisplayName);
        Assert.True(target.RightAscensionDeg < 0.001 || target.RightAscensionDeg > 359.999);
        Assert.Equal(20, target.DeclinationDeg, 6);
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
        var target = new AstrolabeTarget(1, "Zenith", rightAscension, 0, 2);

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
        var risingTarget = new AstrolabeTarget(1, "Rising", localSidereal - 45, 0, 2);
        var settingTarget = new AstrolabeTarget(2, "Setting", localSidereal + 45, 0, 2);

        var rising = AstrolabeService.Read(risingTarget, 0, totalDays, daysPerYear, hoursPerDay, 0);
        var setting = AstrolabeService.Read(settingTarget, 0, totalDays, daysPerYear, hoursPerDay, 0);

        Assert.Equal(AstrolabeMotionState.Rising, rising.MotionState);
        Assert.Equal(AstrolabeMotionState.Setting, setting.MotionState);
    }

    [Fact]
    public void Read_Classifies_Circumpolar_And_Never_Rising_Targets()
    {
        var circumpolar = AstrolabeService.Read(
            new AstrolabeTarget(1, "North", 0, 60, 2),
            latitudeDeg: 60,
            totalDays: 0,
            daysPerYear: 120,
            hoursPerDay: 24,
            longitudeDeg: 0);
        var neverRises = AstrolabeService.Read(
            new AstrolabeTarget(2, "South", 0, -60, 2),
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
}
