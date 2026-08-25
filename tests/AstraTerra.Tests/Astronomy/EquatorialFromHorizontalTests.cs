using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class EquatorialFromHorizontalTests
{
    [Theory]
    [InlineData(80.0, 20.0, 51.0, 90.0)]
    [InlineData(15.0, -35.0, -20.0, 200.0)]
    [InlineData(310.0, 65.0, 51.0, 15.0)]
    [InlineData(0.0, 0.0, 0.0, 0.0)]
    public void Reading_A_Sighting_Back_Into_The_Sky_Returns_Where_It_Came_From(
        double rightAscensionDeg,
        double declinationDeg,
        double latitudeDeg,
        double localSiderealDeg)
    {
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            rightAscensionDeg,
            declinationDeg,
            latitudeDeg,
            localSiderealDeg);

        var equatorial = CelestialMath.GetEquatorialCoordinates(
            horizontal.AzimuthDeg,
            horizontal.AltitudeDeg,
            latitudeDeg,
            localSiderealDeg);

        Assert.Equal(rightAscensionDeg, equatorial.RightAscensionDeg, 6);
        Assert.Equal(declinationDeg, equatorial.DeclinationDeg, 6);
    }
}
