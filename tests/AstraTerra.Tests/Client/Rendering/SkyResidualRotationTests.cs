using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using OpenTK.Mathematics;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public class SkyResidualRotationTests
{
    /// <summary>
    /// The rotation has to agree with the projection itself, or a star drawn between two refreshes
    /// is somewhere the sextant and the astrolabe do not think it is.
    /// </summary>
    [Theory]
    [InlineData(45.0, 0.0, 0.0, 0.05)]
    [InlineData(45.0, 120.0, 60.0, 0.05)]
    [InlineData(45.0, 200.0, -40.0, 3.0)]
    [InlineData(-30.0, 300.0, 20.0, 0.05)]
    [InlineData(-30.0, 45.0, -75.0, 12.0)]
    [InlineData(5.0, 15.0, 5.0, 0.05)]
    [InlineData(70.0, 250.0, 80.0, 0.05)]
    [InlineData(45.0, 10.0, 30.0, -0.05)]
    [InlineData(45.0, 10.0, 30.0, -7.5)]
    public void CarriesAProjectedStarToWhereReprojectingWouldPutIt(
        double latitudeDeg,
        double rightAscensionDeg,
        double declinationDeg,
        double siderealDeltaDeg)
    {
        const double cachedSiderealDeg = 137.0;
        var cached = Project(rightAscensionDeg, declinationDeg, latitudeDeg, cachedSiderealDeg);
        var expected = Project(rightAscensionDeg, declinationDeg, latitudeDeg, cachedSiderealDeg + siderealDeltaDeg);

        var rotated = Vector3.TransformPosition(cached, SkyResidualRotation.Build(latitudeDeg, siderealDeltaDeg));

        Assert.Equal(expected.X, rotated.X, 5);
        Assert.Equal(expected.Y, rotated.Y, 5);
        Assert.Equal(expected.Z, rotated.Z, 5);
    }

    [Fact]
    public void LeavesTheSkyAloneWhenNothingHasTurned()
    {
        Assert.Equal(Matrix4.Identity, SkyResidualRotation.Build(45.0, 0.0));
    }

    /// <summary>A dropped projection caches not-a-number, which must not reach the shader.</summary>
    [Theory]
    [InlineData(double.NaN, 0.05)]
    [InlineData(45.0, double.NaN)]
    public void LeavesTheSkyAloneWithNothingCached(double latitudeDeg, double siderealDeltaDeg)
    {
        Assert.Equal(Matrix4.Identity, SkyResidualRotation.Build(latitudeDeg, siderealDeltaDeg));
    }

    /// <summary>
    /// The rotation turns the sky about the observer's eye, not about the world origin, so the
    /// eye-height offset has to survive it.
    /// </summary>
    [Fact]
    public void KeepsTheEyeHeightOriginUnrotated()
    {
        var model = SkyResidualRotation.Build(45.0, 0.05) * Matrix4.CreateTranslation(0f, 1.7f, 0f);

        Assert.Equal(0f, model.M41, 5);
        Assert.Equal(1.7f, model.M42, 5);
        Assert.Equal(0f, model.M43, 5);
    }

    private static Vector3 Project(
        double rightAscensionDeg,
        double declinationDeg,
        double latitudeDeg,
        double localSiderealDeg)
    {
        var (x, y, z) = SkyProjection.GetWorldDirection(
            new EquatorialCoordinates(rightAscensionDeg, declinationDeg),
            latitudeDeg,
            localSiderealDeg);

        return new Vector3((float)x, (float)y, (float)z);
    }
}
