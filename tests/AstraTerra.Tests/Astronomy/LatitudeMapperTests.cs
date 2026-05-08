using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class LatitudeMapperTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.25, 45.0)]
    [InlineData(0.50, 90.0)]
    [InlineData(0.75, 45.0)]
    public void Repeating_Latitude_Maps_To_Earth_Bands(double normalizedBandPosition, double expectedAbsoluteLatitude)
    {
        var mapped = LatitudeMapper.MapRepeatingLatitude(normalizedBandPosition);
        Assert.Equal(expectedAbsoluteLatitude, Math.Abs(mapped), 6);
    }

    [Theory]
    [InlineData(-1.0, -90.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 90.0)]
    [InlineData(2.0, 90.0)]
    public void Climate_Latitude_Maps_Directly_To_Earth_Latitude(double gameLatitude, double expectedLatitude)
    {
        Assert.Equal(expectedLatitude, LatitudeMapper.MapClimateLatitude(gameLatitude), 6);
    }

    [Fact]
    public void Game_Latitude_Prefers_Vintage_Story_Climate_Callback()
    {
        var mapped = LatitudeMapper.MapGameLatitude(50000, _ => -0.75);

        Assert.Equal(-67.5, mapped, 6);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(25000, 45.0)]
    [InlineData(50000, 90.0)]
    [InlineData(75000, -45.0)]
    [InlineData(100000, 0.0)]
    public void World_Z_Maps_To_Testable_Latitude_Bands(double z, double expectedLatitude)
    {
        Assert.Equal(expectedLatitude, LatitudeMapper.MapWorldZ(z), 6);
    }

    [Theory]
    [InlineData(500000, 500000, 0.0)]
    [InlineData(525000, 500000, 45.0)]
    [InlineData(550000, 500000, 90.0)]
    [InlineData(575000, 500000, -45.0)]
    [InlineData(600000, 500000, 0.0)]
    public void World_Z_Can_Be_Anchored_To_Player_Facing_Origin(double z, double originZ, double expectedLatitude)
    {
        Assert.Equal(expectedLatitude, LatitudeMapper.MapWorldZ(z, originZ), 6);
    }

    [Theory]
    [InlineData(50000, 0.0)]
    [InlineData(100000, 90.0)]
    [InlineData(150000, 180.0)]
    [InlineData(200000, -90.0)]
    [InlineData(250000, 0.0)]
    public void World_X_Maps_To_Repeating_Longitude(double x, double expectedLongitude)
    {
        Assert.Equal(expectedLongitude, LatitudeMapper.MapWorldLongitude(x, mapSizeX: 100000, mapSizeZ: 100000), 6);
    }
}
