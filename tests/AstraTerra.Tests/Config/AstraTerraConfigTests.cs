using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class AstraTerraConfigTests
{
    [Fact]
    public void Longitude_Aware_Sun_Is_Enabled_By_Default()
    {
        Assert.True(new AstraTerraConfig().LongitudeAwareSun);
    }

    [Fact]
    public void Debug_Meteor_Rate_Defaults_To_Real_Time()
    {
        var config = new AstraTerraConfig();

        Assert.Equal(1.0, config.GetDebugMeteorRateMultiplier());
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(30.0, 30.0)]
    [InlineData(101.0, 100.0)]
    public void Debug_Meteor_Rate_Is_Clamped_To_A_Safe_Range(double configured, double expected)
    {
        var config = new AstraTerraConfig { DebugMeteorRateMultiplier = configured };

        Assert.Equal(expected, config.GetDebugMeteorRateMultiplier());
    }

    [Fact]
    public void Non_Finite_Debug_Meteor_Rate_Restores_The_Default()
    {
        var config = new AstraTerraConfig { DebugMeteorRateMultiplier = double.NaN };

        Assert.Equal(1.0, config.GetDebugMeteorRateMultiplier());
    }
}
