using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SolarBandTests
{
    private const double LatitudeDeg = 51.0;
    private const int YearDays = 360;

    [Fact]
    public void A_Band_Watched_For_A_Year_Gives_Back_The_Latitude_And_The_Length_Of_The_Year()
    {
        var band = MarkYear(new SolarBand(), fromDay: 0, days: YearDays + 20);

        var reading = band.Read(SolarEvent.Sunset);

        Assert.True(reading.IsComplete);
        Assert.NotNull(reading.LatitudeDeg);
        Assert.InRange(reading.LatitudeDeg!.Value, LatitudeDeg - 1.5, LatitudeDeg + 1.5);
        Assert.NotNull(reading.YearDays);
        Assert.InRange(reading.YearDays!.Value, YearDays - 10, YearDays + 10);
    }

    [Fact]
    public void The_Sun_Stands_Still_At_The_Turn_Without_Anything_Announcing_It()
    {
        var solsticeDay = 90;
        var notches = Enumerable
            .Range(solsticeDay - 3, 7)
            .Select(day => SolarBandPolicy.Notch(SunsetAzimuthDeg(day)))
            .ToList();

        Assert.True(notches.Distinct().Count() <= 2, "the days around a turn should share a notch");
    }

    [Fact]
    public void An_Edge_Is_Not_A_Turning_Point_Until_A_Later_Mark_Falls_Short_Of_It()
    {
        var band = MarkYear(new SolarBand(), fromDay: 0, days: 80);

        var reading = band.Read(SolarEvent.Sunset);

        Assert.False(reading.IsComplete);
        Assert.Contains("still widening", reading.Describe());
        Assert.Null(reading.LatitudeDeg);
    }

    [Fact]
    public void A_First_Mark_At_An_Extreme_Can_Be_Confirmed_When_That_Extreme_Comes_Round_Again()
    {
        const int northernSolsticeDay = YearDays / 4;
        var band = MarkYear(new SolarBand(), fromDay: northernSolsticeDay, days: YearDays + 20);

        var reading = band.Read(SolarEvent.Sunset);

        Assert.True(reading.IsComplete);
        Assert.NotNull(reading.YearDays);
        Assert.InRange(reading.YearDays!.Value, YearDays - 10, YearDays + 10);
        Assert.True(reading.HighDay > northernSolsticeDay);
    }

    [Fact]
    public void One_End_Found_Reads_As_One_End_Found()
    {
        var band = MarkYear(new SolarBand(), fromDay: 0, days: 140);

        var reading = band.Read(SolarEvent.Sunset);

        Assert.True(reading.HighConfirmed);
        Assert.False(reading.IsComplete);
        Assert.Contains("One end has stopped moving", reading.Describe());
    }

    [Fact]
    public void A_Finished_Band_Says_When_The_Sun_Will_Next_Stand_Still()
    {
        var band = MarkYear(new SolarBand(), fromDay: 0, days: YearDays + 20);
        var reading = band.Read(SolarEvent.Sunset);

        var next = reading.NextTurnDay(fromDay: 400);

        Assert.NotNull(next);
        Assert.True(next >= 400, $"a turn to come, not one already past: {next}");
        Assert.True(next <= 400 + (YearDays / 2), $"within half a year: {next}");
    }

    [Fact]
    public void The_Disc_Binds_To_Where_It_Was_First_Scratched()
    {
        var band = new SolarBand();
        band.Scratch(1, 300.0, SolarEvent.Sunset, LatitudeDeg, x: 10, z: 20);

        var refused = band.Scratch(2, 300.0, SolarEvent.Sunset, LatitudeDeg + 6.0, x: 4000, z: 20);

        Assert.Equal(SolarMarkOutcome.WrongPlace, refused.Outcome);
        Assert.Contains("does not set where this disc was scribed", refused.Message);
        Assert.Single(band.Marks);
        Assert.Equal(LatitudeDeg, band.BoundLatitudeDeg);
        Assert.Equal(10, band.BoundX);
    }

    [Fact]
    public void A_Day_Already_Scratched_Cannot_Be_Scratched_Again()
    {
        var band = new SolarBand();
        band.Scratch(1, 300.0, SolarEvent.Sunset, LatitudeDeg, 0, 0);

        var second = band.Scratch(1, 301.0, SolarEvent.Sunset, LatitudeDeg, 0, 0);

        Assert.Equal(SolarMarkOutcome.AlreadyMarked, second.Outcome);
        Assert.Single(band.Marks);
    }

    [Fact]
    public void The_Two_Rims_Are_Kept_Apart()
    {
        var band = new SolarBand();
        band.Scratch(1, 300.0, SolarEvent.Sunset, LatitudeDeg, 0, 0);
        band.Scratch(1, 60.0, SolarEvent.Sunrise, LatitudeDeg, 0, 0);

        Assert.Equal(1, band.Read(SolarEvent.Sunset).MarkCount);
        Assert.Equal(1, band.Read(SolarEvent.Sunrise).MarkCount);
    }

    [Fact]
    public void An_Empty_Rim_Says_So()
    {
        Assert.Contains("No marks", new SolarBand().Read(SolarEvent.Sunrise).Describe());
    }

    [Fact]
    public void A_Disc_Keeps_Its_Marks_And_Its_Place_Through_Json()
    {
        var band = MarkYear(new SolarBand(), fromDay: 0, days: 40);

        var loaded = SolarBandPersistence.Deserialize(SolarBandPersistence.Serialize(band));

        Assert.Equal(band.Marks, loaded.Marks);
        Assert.Equal(band.BoundLatitudeDeg, loaded.BoundLatitudeDeg);
        Assert.Equal(band.BoundZ, loaded.BoundZ);
    }

    [Theory]
    [InlineData(0.0, 46.879)]
    [InlineData(45.0, 68.464)]
    [InlineData(51.0, 78.407)]
    [InlineData(60.0, 105.415)]
    public void The_Width_Of_The_Band_Is_A_Clean_Function_Of_Latitude(double latitudeDeg, double expectedSwingDeg)
    {
        var swing = SolarBandPolicy.SwingDegAt(latitudeDeg);

        Assert.NotNull(swing);
        Assert.Equal(expectedSwingDeg, swing!.Value, 3);
        Assert.Equal(latitudeDeg, SolarBandPolicy.LatitudeFromSwingDeg(swing.Value)!.Value, 4);
    }

    [Fact]
    public void Past_The_Polar_Circle_There_Is_No_Band_To_Measure()
    {
        Assert.Null(SolarBandPolicy.SwingDegAt(70.0));
    }

    [Fact]
    public void A_Copper_Rim_Finds_The_Same_Year_On_A_Coarser_Notch()
    {
        var copper = MarkYear(new SolarBand(SolarBandPolicy.CoarseArcNotchDeg), fromDay: 0, days: YearDays + 20);

        var reading = copper.Read(SolarEvent.Sunset);

        // P1: the softer metal costs precision and patience, never the discovery itself.
        Assert.True(reading.IsComplete);
        Assert.NotNull(reading.LatitudeDeg);
        Assert.InRange(reading.LatitudeDeg!.Value, LatitudeDeg - 4.0, LatitudeDeg + 4.0);
        Assert.All(
            copper.Marks,
            mark => Assert.Equal(0.0, mark.NotchDeg % SolarBandPolicy.CoarseArcNotchDeg, 6));
    }

    [Fact]
    public void A_Rim_Keeps_Its_Own_Scale_Across_A_Save()
    {
        var copper = new SolarBand(SolarBandPolicy.CoarseArcNotchDeg);
        copper.Scratch(400, 241.3, SolarEvent.Sunset, LatitudeDeg, 0, 0);

        var loaded = SolarBandPersistence.Deserialize(SolarBandPersistence.Serialize(copper));

        Assert.Equal(SolarBandPolicy.CoarseArcNotchDeg, loaded.NotchDeg);
        Assert.Equal(240.0, loaded.Marks.Single().NotchDeg);
    }

    [Fact]
    public void A_Disc_Scratched_Before_There_Were_Two_Metals_Is_A_Bronze_One()
    {
        const string LegacyJson = """
        {
          "schemaVersion": 1,
          "boundLatitudeDeg": 51.0,
          "boundX": 0,
          "boundZ": 0,
          "marks": [{ "day": 400, "notchDeg": 240.0, "event": 1 }]
        }
        """;

        var loaded = SolarBandPersistence.Deserialize(LegacyJson);

        Assert.Equal(SolarBandPolicy.ArcNotchDeg, loaded.NotchDeg);
        Assert.Single(loaded.Marks);
    }

    private static SolarBand MarkYear(SolarBand band, int fromDay, int days)
    {
        foreach (var day in Enumerable.Range(fromDay, days))
        {
            band.Scratch(day, SunsetAzimuthDeg(day), SolarEvent.Sunset, LatitudeDeg, 0, 0);
        }

        return band;
    }

    /// <summary>Where the sun goes down on a given day, from the sunset condition itself.</summary>
    private static double SunsetAzimuthDeg(int day)
    {
        var declination = SolarBandPolicy.ObliquityDeg
                          * Math.Sin(2.0 * Math.PI * day / YearDays);
        var ratio = Math.Sin(ToRadians(declination)) / Math.Cos(ToRadians(LatitudeDeg));
        return 360.0 - ToDegrees(Math.Acos(Math.Clamp(ratio, -1.0, 1.0)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
