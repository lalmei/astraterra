using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class SkyBodyModelTests
{
    [Theory]
    [InlineData(0, 1, 0, 90.0)]
    [InlineData(0, 0, -1, 0.0)]
    [InlineData(0, -1, 0, -90.0)]
    public void Altitude_Comes_From_The_Vertical_Component(double x, double y, double z, double expectedAltitude)
    {
        var body = SkyBodyModel.FromWorldDirection("Sun", x, y, z);

        Assert.NotNull(body);
        Assert.Equal(expectedAltitude, body!.AltitudeDeg, 6);
    }

    [Theory]
    [InlineData(0, 0, -1, 0.0)]    // north is -Z
    [InlineData(1, 0, 0, 90.0)]    // east is +X
    [InlineData(0, 0, 1, 180.0)]   // south is +Z
    [InlineData(-1, 0, 0, 270.0)]  // west is -X
    public void Azimuth_Is_Measured_Clockwise_From_North(double x, double y, double z, double expectedAzimuth)
    {
        var body = SkyBodyModel.FromWorldDirection("Moon", x, y, z);

        Assert.NotNull(body);
        Assert.Equal(expectedAzimuth, body!.AzimuthDeg, 6);
    }

    [Theory]
    [InlineData(92.5, 8.1)]
    [InlineData(180.0, 49.0)]
    [InlineData(267.5, 8.1)]
    public void Recovers_The_Angles_Vintage_Story_Encoded(double azimuthDeg, double altitudeDeg)
    {
        // Vanilla's zenith angle is 2pi - acos(sin(altitude)), which sits in the fourth quadrant
        // where sine is negative. That negative factor is what makes its vector match AstraTerra's
        // convention, so no 180 degree rotation belongs here. Building the vector the way
        // GetSunPosition does guards against "correcting" that away.
        var (x, y, z) = VanillaSunVector(azimuthDeg, altitudeDeg);

        var body = SkyBodyModel.FromWorldDirection("Sun", x, y, z);

        Assert.NotNull(body);
        Assert.Equal(azimuthDeg, body!.AzimuthDeg, 6);
        Assert.Equal(altitudeDeg, body.AltitudeDeg, 6);
    }

    /// <summary>Mirrors <c>GameCalendar.GetSunPosition</c>, zenith convention included.</summary>
    private static (double X, double Y, double Z) VanillaSunVector(double azimuthDeg, double altitudeDeg)
    {
        var sinAltitude = Math.Sin(altitudeDeg * Math.PI / 180.0);
        var zenith = (2.0 * Math.PI) - Math.Acos(sinAltitude);
        var azimuth = (2.0 * Math.PI) - (azimuthDeg * Math.PI / 180.0);
        var horizontal = Math.Sin(zenith);
        return (horizontal * Math.Sin(azimuth), Math.Cos(zenith), horizontal * Math.Cos(azimuth));
    }

    [Fact]
    public void Scaled_Vectors_Give_The_Same_Angles()
    {
        var unit = SkyBodyModel.FromWorldDirection("Sun", 0.5, 0.5, -0.5);
        var scaled = SkyBodyModel.FromWorldDirection("Sun", 25.0, 25.0, -25.0);

        Assert.NotNull(unit);
        Assert.NotNull(scaled);
        Assert.Equal(unit!.AltitudeDeg, scaled!.AltitudeDeg, 6);
        Assert.Equal(unit.AzimuthDeg, scaled.AzimuthDeg, 6);
    }

    [Fact]
    public void Degenerate_Vector_Is_Not_Sightable()
    {
        Assert.Null(SkyBodyModel.FromWorldDirection("Sun", 0, 0, 0));
    }

    [Theory]
    [InlineData(35.0, 20.0)]
    [InlineData(210.0, 5.0)]
    [InlineData(300.0, 72.0)]
    public void Round_Trips_A_Projected_Star_Back_To_Its_Own_Angles(double azimuthDeg, double altitudeDeg)
    {
        // A star projected to a world direction and read back must land on the same angles, which
        // is what keeps a sun or moon sighting comparable with a star sighting.
        var direction = DirectionFromHorizontal(azimuthDeg, altitudeDeg);
        var body = SkyBodyModel.FromWorldDirection("Star", direction.X, direction.Y, direction.Z);

        Assert.NotNull(body);
        Assert.Equal(azimuthDeg, body!.AzimuthDeg, 6);
        Assert.Equal(altitudeDeg, body.AltitudeDeg, 6);
    }

    [Fact]
    public void Star_Sightings_Keep_Their_Catalog_Identity()
    {
        var star = StarRenderModel.Project(
            new StarCatalogEntry(32349, 101.3, -16.7, -1.46, null, true),
            latitudeDeg: 20,
            localSiderealDeg: 101.3,
            brightnessBias: 1.0);
        Assert.NotNull(star);

        var body = SkyBodyModel.FromStar(star!);

        Assert.Equal("Star HIP 32349", body.DisplayName);
        Assert.Equal(star!.AltitudeDeg, body.AltitudeDeg, 6);
        Assert.Equal(star.AzimuthDeg, body.AzimuthDeg, 6);
        Assert.Equal(star.DirectionX, body.DirectionX, 6);
        Assert.Equal(star.DirectionY, body.DirectionY, 6);
        Assert.Equal(star.DirectionZ, body.DirectionZ, 6);
    }

    [Fact]
    public void A_Planet_Sights_By_Name_Rather_Than_A_Catalogue_Number()
    {
        var catalog = PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));
        var mars = catalog.Planets.Single(planet => planet.Id == "mars");
        var rendered = PlanetRenderModel.Project(
            mars,
            new FixedEphemeris(new EquatorialCoordinates(101.3, -16.7), visualMagnitude: -1.2),
            totalDays: 0,
            latitudeDeg: 20,
            localSiderealDeg: 101.3,
            brightnessBias: 1.0);
        Assert.NotNull(rendered);

        var body = SkyBodyModel.FromBody(rendered!.DisplayName, rendered.Body);

        Assert.Equal("Mars", body.DisplayName);
        Assert.Equal(rendered.AltitudeDeg, body.AltitudeDeg, 6);
        Assert.Equal(rendered.AzimuthDeg, body.AzimuthDeg, 6);
    }

    /// <summary>Mirrors the direction convention used when stars are placed in the sky.</summary>
    private static (double X, double Y, double Z) DirectionFromHorizontal(double azimuthDeg, double altitudeDeg)
    {
        var azimuth = azimuthDeg * Math.PI / 180.0;
        var altitude = altitudeDeg * Math.PI / 180.0;
        var horizontal = Math.Cos(altitude);
        return (horizontal * Math.Sin(azimuth), Math.Sin(altitude), -horizontal * Math.Cos(azimuth));
    }
}
