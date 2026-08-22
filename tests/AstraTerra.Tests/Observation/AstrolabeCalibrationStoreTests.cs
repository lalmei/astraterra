using AstraTerra.Observation;
using Vintagestory.API.Datastructures;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class AstrolabeCalibrationStoreTests
{
    [Fact]
    public void Write_Then_Read_Returns_The_Plate()
    {
        var attributes = new TreeAttribute();

        AstrolabeCalibrationStore.Write(attributes, latitudeDeg: 43.25, totalDays: 118.5);

        Assert.True(AstrolabeCalibrationStore.IsCalibrated(attributes));
        Assert.Equal(43.25, AstrolabeCalibrationStore.ReadLatitude(attributes)!.Value, 6);
        Assert.Equal(118.5, AstrolabeCalibrationStore.ReadDay(attributes)!.Value, 6);
    }

    /// <summary>
    /// Zero is a latitude, so an uncalibrated instrument cannot be represented by one. Every
    /// astrolabe that existed before plates did reads as uncalibrated by this test, which is what
    /// sends its owner out to sight the pole once.
    /// </summary>
    [Fact]
    public void An_Untouched_Instrument_Is_Uncalibrated_Not_Equatorial()
    {
        var attributes = new TreeAttribute();

        Assert.False(AstrolabeCalibrationStore.IsCalibrated(attributes));
        Assert.Null(AstrolabeCalibrationStore.ReadLatitude(attributes));
        Assert.Null(AstrolabeCalibrationStore.ReadDay(attributes));

        AstrolabeCalibrationStore.Write(attributes, latitudeDeg: 0.0, totalDays: 4.0);

        Assert.True(AstrolabeCalibrationStore.IsCalibrated(attributes));
        Assert.Equal(0.0, AstrolabeCalibrationStore.ReadLatitude(attributes)!.Value);
    }

    [Fact]
    public void Reading_A_Missing_Stack_Is_Uncalibrated_Rather_Than_A_Throw()
    {
        Assert.False(AstrolabeCalibrationStore.IsCalibrated((ITreeAttribute?)null));
        Assert.Null(AstrolabeCalibrationStore.ReadLatitude((ITreeAttribute?)null));
        Assert.Null(AstrolabeCalibrationStore.ReadDay((ITreeAttribute?)null));
    }

    [Fact]
    public void Recutting_Replaces_The_Previous_Plate()
    {
        var attributes = new TreeAttribute();
        AstrolabeCalibrationStore.Write(attributes, latitudeDeg: 12.0, totalDays: 3.0);

        AstrolabeCalibrationStore.Write(attributes, latitudeDeg: -55.0, totalDays: 90.0);

        Assert.Equal(-55.0, AstrolabeCalibrationStore.ReadLatitude(attributes)!.Value, 6);
        Assert.Equal(90.0, AstrolabeCalibrationStore.ReadDay(attributes)!.Value, 6);
    }

    /// <summary>
    /// A latitude past the pole would be taken as real by the horizontal-coordinate maths and come
    /// back as a plausible, wrong sky rather than as an error anyone could trace.
    /// </summary>
    [Theory]
    [InlineData(1000.0, 90.0)]
    [InlineData(-1000.0, -90.0)]
    [InlineData(90.0, 90.0)]
    public void Latitude_Is_Clamped_To_The_Poles(double written, double expected)
    {
        var attributes = new TreeAttribute();

        AstrolabeCalibrationStore.Write(attributes, written, totalDays: 1.0);

        Assert.Equal(expected, AstrolabeCalibrationStore.ReadLatitude(attributes)!.Value, 6);
    }
}
