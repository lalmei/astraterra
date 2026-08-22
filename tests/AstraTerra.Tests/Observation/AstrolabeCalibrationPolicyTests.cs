using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class AstrolabeCalibrationPolicyTests
{
    [Theory]
    [InlineData(45.0, 45.0, AstrolabePlateFit.OnPlate)]
    [InlineData(45.0, 46.4, AstrolabePlateFit.OnPlate)]
    [InlineData(45.0, 43.6, AstrolabePlateFit.OnPlate)]
    [InlineData(45.0, 50.0, AstrolabePlateFit.Drifting)]
    [InlineData(45.0, 37.0, AstrolabePlateFit.Drifting)]
    [InlineData(45.0, 60.0, AstrolabePlateFit.OffPlate)]
    [InlineData(-30.0, 10.0, AstrolabePlateFit.OffPlate)]
    public void Fit_Grades_The_Gap_Between_Plate_And_Observer(double plate, double observer, AstrolabePlateFit expected)
    {
        Assert.Equal(expected, AstrolabeCalibrationPolicy.Fit(plate, observer));
    }

    /// <summary>
    /// Drift is a distance, not a direction: an observer south of the plate is as badly served as one
    /// the same distance north, and the classification must not depend on which hemisphere is which.
    /// </summary>
    [Fact]
    public void Drift_Is_Unsigned_And_Symmetric()
    {
        Assert.Equal(12.0, AstrolabeCalibrationPolicy.DriftDeg(40.0, 52.0), 6);
        Assert.Equal(12.0, AstrolabeCalibrationPolicy.DriftDeg(52.0, 40.0), 6);
        Assert.Equal(
            AstrolabeCalibrationPolicy.Fit(-20.0, -40.0),
            AstrolabeCalibrationPolicy.Fit(20.0, 40.0));
    }

    [Fact]
    public void Calibrating_Needs_Open_Sky_And_A_Dark_One()
    {
        Assert.True(AstrolabeCalibrationPolicy.CanCalibrate(canSeeSky: true, daylightStrength: 0.0));
        Assert.False(AstrolabeCalibrationPolicy.CanCalibrate(canSeeSky: false, daylightStrength: 0.0));
        Assert.False(AstrolabeCalibrationPolicy.CanCalibrate(canSeeSky: true, daylightStrength: 1.0));
    }

    /// <summary>
    /// The floor matches the one the sextant sights stars under, so "bright enough to sight a star"
    /// and "bright enough to cut a plate" cannot drift apart into two different dusks.
    /// </summary>
    [Fact]
    public void The_Darkness_Floor_Is_The_Star_Sighting_Floor()
    {
        Assert.Equal(0.02, AstrolabeCalibrationPolicy.DarknessFloor);
        Assert.True(AstrolabeCalibrationPolicy.CanCalibrate(canSeeSky: true, daylightStrength: 0.97));
        Assert.False(AstrolabeCalibrationPolicy.CanCalibrate(canSeeSky: true, daylightStrength: 0.985));
    }

    [Fact]
    public void Each_Obstacle_Names_Its_Own_Fix()
    {
        Assert.Null(AstrolabeCalibrationPolicy.DescribeObstacle(canSeeSky: true, daylightStrength: 0.0));

        var roofed = AstrolabeCalibrationPolicy.DescribeObstacle(canSeeSky: false, daylightStrength: 0.0);
        var daylit = AstrolabeCalibrationPolicy.DescribeObstacle(canSeeSky: true, daylightStrength: 1.0);
        Assert.Contains("cover", roofed);
        Assert.Contains("dusk", daylit);
        Assert.NotEqual(roofed, daylit);

        // Roofed at noon reports the roof, because that is the obstacle the player can do something
        // about right now.
        Assert.Equal(roofed, AstrolabeCalibrationPolicy.DescribeObstacle(canSeeSky: false, daylightStrength: 1.0));
    }

    [Fact]
    public void Progress_Runs_Zero_To_One_Across_The_Sighting_And_Clamps()
    {
        Assert.Equal(0f, AstrolabeCalibrationPolicy.Progress(0f));
        Assert.Equal(0.5f, AstrolabeCalibrationPolicy.Progress(AstrolabeCalibrationPolicy.CalibrationSeconds / 2f), 3);
        Assert.Equal(1f, AstrolabeCalibrationPolicy.Progress(AstrolabeCalibrationPolicy.CalibrationSeconds));
        Assert.Equal(1f, AstrolabeCalibrationPolicy.Progress(AstrolabeCalibrationPolicy.CalibrationSeconds * 10f));
        Assert.Equal(0f, AstrolabeCalibrationPolicy.Progress(-5f));

        Assert.False(AstrolabeCalibrationPolicy.IsComplete(AstrolabeCalibrationPolicy.CalibrationSeconds - 0.01f));
        Assert.True(AstrolabeCalibrationPolicy.IsComplete(AstrolabeCalibrationPolicy.CalibrationSeconds));
    }

    [Fact]
    public void Latitude_Reads_With_A_Hemisphere_Except_At_The_Equator()
    {
        Assert.Equal("latitude 43.2° N", AstrolabeCalibrationPolicy.FormatLatitude(43.24));
        Assert.Equal("latitude 43.2° S", AstrolabeCalibrationPolicy.FormatLatitude(-43.24));
        Assert.Equal("latitude 0.0°", AstrolabeCalibrationPolicy.FormatLatitude(0.0));
        Assert.Equal("latitude 0.0°", AstrolabeCalibrationPolicy.FormatLatitude(-0.02));
    }

    /// <summary>
    /// The HUD line is the only place a player learns that the instrument is answering about
    /// somewhere else, so it has to say the plate's latitude always, and how far they have walked
    /// from it as soon as that is true.
    /// </summary>
    [Fact]
    public void The_Plate_Line_Stays_Quiet_Until_The_Player_Has_Travelled()
    {
        Assert.Equal("plate latitude 45.0° N", AstrolabeCalibrationPolicy.DescribePlate(45.0, 45.0));

        var drifting = AstrolabeCalibrationPolicy.DescribePlate(45.0, 50.0);
        Assert.Contains("plate latitude 45.0° N", drifting);
        Assert.Contains("5.0° north of it", drifting);
        Assert.DoesNotContain("recut", drifting);

        var offPlate = AstrolabeCalibrationPolicy.DescribePlate(45.0, 20.0);
        Assert.Contains("25.0° south of it", offPlate);
        Assert.Contains("recut it", offPlate);
    }
}
