using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class LatitudeMapperTests
{
    private const double DefaultPolarEquatorDistance = WorldClimateScale.DefaultPolarEquatorDistance;

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
    [InlineData(125000, 0.0)]
    [InlineData(150000, 45.0)]
    [InlineData(175000, 90.0)]
    [InlineData(200000, 135.0)]
    [InlineData(225000, 180.0)]
    [InlineData(100000, -45.0)]
    [InlineData(75000, -90.0)]
    public void World_X_Maps_To_Repeating_Longitude_At_Default_Polar_Distance(double x, double expectedLongitude)
    {
        Assert.Equal(
            expectedLongitude,
            LatitudeMapper.MapWorldLongitude(x, mapSizeX: 250000, DefaultPolarEquatorDistance),
            6);
    }

    [Fact]
    public void Longitude_Degrees_Per_Block_Do_Not_Depend_On_Map_Size()
    {
        const double mapSizeSmall = 250000;
        const double mapSizeLarge = 1024000;
        const double offsetWest = -50000;

        var smallX = (mapSizeSmall * 0.5) + offsetWest;
        var largeX = (mapSizeLarge * 0.5) + offsetWest;

        Assert.Equal(
            LatitudeMapper.MapWorldLongitude(smallX, mapSizeSmall, DefaultPolarEquatorDistance),
            LatitudeMapper.MapWorldLongitude(largeX, mapSizeLarge, DefaultPolarEquatorDistance),
            6);
    }

    [Fact]
    public void A_Shorter_Polar_Distance_Makes_Longitude_Change_Faster()
    {
        const double mapSizeX = 200000;
        const double x = 130000;
        var atDefault = LatitudeMapper.MapWorldLongitude(x, mapSizeX, DefaultPolarEquatorDistance);
        var atHalfScale = LatitudeMapper.MapWorldLongitude(x, mapSizeX, polarEquatorDistance: 25000);

        Assert.True(Math.Abs(atHalfScale) > Math.Abs(atDefault));
    }
}
