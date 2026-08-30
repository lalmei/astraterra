using AstraTerra.Astronomy;
using AstraTerra.Observation;
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

        var body = SkyBodyModel.FromStar(star!.Value);

        Assert.Equal("Star HIP 32349", body.DisplayName);
        Assert.Equal(star!.Value.AltitudeDeg, body.AltitudeDeg, 6);
        Assert.Equal(star.Value.AzimuthDeg, body.AzimuthDeg, 6);
        Assert.Equal(star.Value.DirectionX, body.DirectionX, 6);
        Assert.Equal(star.Value.DirectionY, body.DirectionY, 6);
        Assert.Equal(star.Value.DirectionZ, body.DirectionZ, 6);
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

    /// <summary>
    /// The sky log's camera altitude used to read -89 wherever the player looked, because vanilla's
    /// pitch is measured down from the zenith rather than up from the horizon. A reading that is
    /// always the same number is worse than no reading: it looks like data.
    /// </summary>
    [Theory]
    [InlineData(Math.PI / 2, 90.0)]
    [InlineData(Math.PI, 0.0)]
    [InlineData(3 * Math.PI / 2, -90.0)]
    public void Camera_Altitude_Reads_Up_From_The_Horizon(double pitchRadians, double expectedDeg)
    {
        Assert.Equal(expectedDeg, SkyBodyModel.GetCameraAltitudeDeg(pitchRadians), precision: 6);
    }

    [Fact]
    public void Lying_On_Your_Back_Reads_As_Very_Nearly_Straight_Up()
    {
        var altitude = SkyBodyModel.GetCameraAltitudeDeg(SkyLyingPolicy.LookUpPitch);

        Assert.InRange(altitude, 89.0, 90.0);
    }
    /// <summary>
    /// A near body reaches the sextant as the disc it is, carrying its own width: without that the
    /// sight would only latch within a reticle of the centre of a body tens of degrees across.
    /// </summary>
    [Fact]
    public void A_Near_Body_Sights_As_A_Disc_With_Its_Own_Width()
    {
        var placed = NearBodyRenderModel.Place(
            Giant(),
            totalDays: 3.0,
            latitudeDeg: 15.0,
            localSiderealDeg: 40.0,
            new SkyDirection(0.0, 1.0, 0.0));

        Assert.NotNull(placed);
        var sighted = SkyBodyModel.FromNearBody(placed!);

        Assert.Equal("Warden", sighted.DisplayName);
        Assert.Equal(11.0, sighted.AngularRadiusDeg, 9);
        Assert.Equal(placed!.AltitudeDeg, sighted.AltitudeDeg, 9);
        Assert.Equal(placed.Direction.X, sighted.DirectionX, 9);
        Assert.Equal(placed.Direction.Y, sighted.DirectionY, 9);
        Assert.Equal(placed.Direction.Z, sighted.DirectionZ, 9);

        // No magnitude: a lit disc has no place on a point source's brightness scale.
        Assert.Null(sighted.VisualMagnitude);
    }

    /// <summary>Azimuth has to come out on the same convention every other sighting uses.</summary>
    [Fact]
    public void A_Near_Body_Reports_Azimuth_Clockwise_From_North()
    {
        var placed = NearBodyRenderModel.Place(
            Giant(),
            totalDays: 0.0,
            latitudeDeg: 0.0,
            localSiderealDeg: 0.0,
            new SkyDirection(0.0, 1.0, 0.0));

        Assert.NotNull(placed);
        var sighted = SkyBodyModel.FromNearBody(placed!);
        var fromDirection = SkyBodyModel.FromWorldDirection(
            "Warden",
            placed!.Direction.X,
            placed.Direction.Y,
            placed.Direction.Z);

        Assert.NotNull(fromDirection);
        Assert.Equal(fromDirection!.AzimuthDeg, sighted.AzimuthDeg, 9);
        Assert.Equal(fromDirection.AltitudeDeg, sighted.AltitudeDeg, 6);
    }

    private static NearBodyEntry Giant()
        => new(
            "parent",
            "Warden",
            NearBodyKind.ParentPlanet,
            AngularDiameterDeg: 22.0,
            HourAngleDeg: 20.0,
            HourAngleRateDegPerDay: 0.0,
            DeclinationDeg: 5.0,
            Brightness: 1.0,
            new NearBodyFace(2, new int[4], DiscFraction: 1.0));
}
