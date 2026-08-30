using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class PlanetDiscRenderModelTests
{
    private const int DaysPerYear = 12;
    private const double LatitudeDeg = 20.0;

    [Fact]
    public void The_Face_Drawn_Is_The_One_Nearest_The_Phase_The_Planet_Is_Showing()
    {
        var faces = new[]
        {
            new PlanetFaceEntry(0.0, "full"),
            new PlanetFaceEntry(90.0, "half"),
            new PlanetFaceEntry(180.0, "crescent")
        };

        Assert.Equal("full", PlanetDiscRenderModel.SelectFace(faces, 10.0).TexturePath);
        Assert.Equal("half", PlanetDiscRenderModel.SelectFace(faces, 70.0).TexturePath);
        Assert.Equal("crescent", PlanetDiscRenderModel.SelectFace(faces, 170.0).TexturePath);
    }

    /// <summary>
    /// The picture has to arrive on the sky the size it was asked for, square, and the right way up:
    /// the corners come out bottom-left first so the mesh builder's V flip lands the image's own
    /// bottom row there.
    /// </summary>
    [Fact]
    public void A_Disc_Is_A_Square_Of_The_Asked_For_Width_Around_The_Body()
    {
        var direction = new SkyDirection(0.0, 0.0, -1.0);
        var right = new SkyDirection(1.0, 0.0, 0.0);

        var corners = SkyTangent.BuildQuad(direction, right, widthDeg: 2.0);

        Assert.Equal(4, corners.Count);
        Assert.All(corners, corner => Assert.Equal(1.0, Length(corner), 9));
        // A corner of a two-degree square is a degree out along each axis, so half a diagonal away.
        Assert.All(corners, corner => Assert.Equal(Math.Sqrt(2.0), AngleDeg(corner, direction), 2));

        // Bottom-left, bottom-right, top-right, top-left.
        Assert.True(corners[0].X < 0 && corners[0].Y < 0);
        Assert.True(corners[1].X > 0 && corners[1].Y < 0);
        Assert.True(corners[2].X > 0 && corners[2].Y > 0);
        Assert.True(corners[3].X < 0 && corners[3].Y > 0);
    }

    [Fact]
    public void A_Planet_Is_Drawn_As_Wide_As_Its_Globe_Actually_Subtends()
    {
        var catalog = Catalog(Jupiter());
        var model = new PlanetDiscRenderModel(catalog, DaysPerYear);

        var placed = PlaceAll(model, totalDays: 3.0);
        var jupiter = placed.Single(disc => disc.Id == "jupiter");

        var ephemeris = new PlanetEphemeris(Jupiter(), catalog.Observer, DaysPerYear);
        var distanceKm = ephemeris.GetGeocentricPosition(3.0).Length
            * PlanetDiscRenderModel.KilometresPerAstronomicalUnit;
        var expected = 142_984.0 / distanceKm * 180.0 / Math.PI * PlanetDiscRenderModel.DiscExaggeration;

        Assert.Equal(expected, jupiter.AngularWidthDeg, 9);

        // Which is a hundredth of a degree of real sky, enlarged: recognisable in an eyepiece,
        // and nowhere near large enough to be mistaken for the moon.
        Assert.InRange(jupiter.AngularWidthDeg, 0.03, 0.15);
    }

    /// <summary>
    /// Rings live in the margin of Saturn's picture, so the image is drawn wider than the globe by
    /// exactly the factor the catalog gives — and the globe underneath it stays the size it would be
    /// with no rings at all.
    /// </summary>
    [Fact]
    public void A_Ringed_Planet_Draws_Its_Picture_Wider_Than_Its_Globe()
    {
        var bare = Jupiter();
        var ringed = Jupiter() with
        {
            Id = "ringed",
            Disc = bare.Disc! with { ImageWidthInDiameters = 2.27 }
        };
        var model = new PlanetDiscRenderModel(Catalog(bare, ringed), DaysPerYear);

        var placed = PlaceAll(model, totalDays: 3.0);

        Assert.Equal(
            placed.Single(disc => disc.Id == "jupiter").AngularWidthDeg * 2.27,
            placed.Single(disc => disc.Id == "ringed").AngularWidthDeg,
            9);
    }

    [Fact]
    public void A_Moon_Sits_Beside_Its_Planet_And_The_Far_One_Sits_Further_Out()
    {
        var model = new PlanetDiscRenderModel(Catalog(Jupiter()), DaysPerYear);

        var placed = PlaceAll(model, totalDays: 3.0);
        var jupiter = placed.Single(disc => disc.Id == "jupiter");
        var inner = placed.Single(disc => disc.Id == "inner");
        var outer = placed.Single(disc => disc.Id == "outer");

        Assert.Equal("jupiter", inner.ParentId);
        Assert.Null(jupiter.ParentId);

        var innerElongation = Elongation(inner, jupiter);
        var outerElongation = Elongation(outer, jupiter);
        Assert.True(outerElongation > innerElongation);

        // Both are inside the field of the finest telescope here, which is what the exaggeration is
        // chosen to hold: a moon that has left the eyepiece cannot be counted.
        Assert.InRange(outerElongation, innerElongation, 1.8);
    }

    /// <summary>
    /// A moon is a point of light at any magnification a telescope reaches, so it is drawn at the
    /// floor rather than at its own vanishing angular size.
    /// </summary>
    [Fact]
    public void A_Moon_Is_Drawn_At_The_Size_The_Stars_Beside_It_Are()
    {
        var model = new PlanetDiscRenderModel(Catalog(Jupiter()), DaysPerYear);

        var moon = PlaceAll(model, totalDays: 3.0).Single(disc => disc.Id == "outer");

        Assert.Equal(PlanetDiscRenderModel.MinimumMoonWidthDeg, moon.AngularWidthDeg, 9);
    }

    /// <summary>
    /// Half of every orbit is spent behind the planet, and a moon there is not drawn — which is what
    /// makes the count of moons beside Jupiter change over a night rather than sitting at four.
    /// </summary>
    [Fact]
    public void A_Moon_Passing_Behind_Its_Planet_Is_Not_Drawn()
    {
        var model = new PlanetDiscRenderModel(Catalog(Jupiter()), DaysPerYear);
        var period = Jupiter().Moons!.Single(moon => moon.Id == "inner").OrbitalPeriodRealDays;
        var worldDaysPerOrbit = period / WorldEpoch.RealDaysPerWorldYear * DaysPerYear;

        // A quarter turn either side of the crossing: one in front of the planet, one behind it.
        var occultations = 0;
        for (var step = 0; step < 8; step++)
        {
            var placed = PlaceAll(model, 3.0 + (worldDaysPerOrbit * step / 8.0));
            if (placed.All(disc => disc.Id != "inner"))
            {
                occultations++;
            }
        }

        Assert.InRange(occultations, 1, 4);
    }

    private static IReadOnlyList<RenderedPlanetDisc> PlaceAll(PlanetDiscRenderModel model, double totalDays)
    {
        // Straight overhead at the equator, with the sun to one side, so nothing is lost to the
        // horizon: this is about where the bodies land relative to each other.
        var placed = new List<RenderedPlanetDisc>();
        for (var siderealDeg = 0.0; siderealDeg < 360.0; siderealDeg += 15.0)
        {
            model.Place(totalDays, LatitudeDeg, siderealDeg, brightnessBias: 1.0, Sun, placed);
            if (placed.Any(disc => disc.ParentId is null))
            {
                return placed.ToList();
            }
        }

        throw new InvalidOperationException("No planet rose anywhere in the day.");
    }

    private static SkyDirection Sun => new(1.0, 0.2, 0.0);

    private static double Elongation(RenderedPlanetDisc moon, RenderedPlanetDisc planet)
        => AngleDeg(Centre(moon), Centre(planet));

    private static DeepSkyDirection Centre(RenderedPlanetDisc disc)
    {
        var x = disc.QuadCorners.Sum(corner => corner.X) / disc.QuadCorners.Count;
        var y = disc.QuadCorners.Sum(corner => corner.Y) / disc.QuadCorners.Count;
        var z = disc.QuadCorners.Sum(corner => corner.Z) / disc.QuadCorners.Count;
        return new DeepSkyDirection(x, y, z);
    }

    private static double Length(DeepSkyDirection direction)
        => Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));

    private static double AngleDeg(DeepSkyDirection left, DeepSkyDirection right)
    {
        var dot = ((left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z))
            / (Length(left) * Length(right));
        return Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
    }

    private static double AngleDeg(DeepSkyDirection corner, SkyDirection direction)
        => AngleDeg(corner, new DeepSkyDirection(direction.X, direction.Y, direction.Z));

    private static PlanetCatalog Catalog(params PlanetEntry[] planets)
        => new(1, Observer, planets);

    private static KeplerianElements Observer => new(
        1.00000261, 0.00000562,
        0.01671123, -0.00004392,
        -0.00001531, -0.01294668,
        100.46457166, 35999.37244981,
        102.93768193, 0.32327364,
        0.0, 0.0);

    private static PlanetEntry Jupiter() => new(
        "jupiter",
        "Jupiter",
        new KeplerianElements(
            5.20288700, -0.00011607,
            0.04838624, -0.00013253,
            1.30439695, -0.00183714,
            34.39644051, 3034.74612775,
            14.72847983, 0.21252668,
            100.47390909, 0.20469106),
        -9.40,
        0.005,
        1.0f,
        0.93f,
        0.78f,
        new PlanetDiscEntry(142_984.0, 1.0, [new PlanetFaceEntry(0.0, "jupiter-0")]),
        [
            new PlanetMoonEntry("inner", "Inner", 421_700.0, 1.769, 3643.0, 0.0, 0.95, "inner"),
            new PlanetMoonEntry("outer", "Outer", 1_882_709.0, 16.689, 4821.0, 90.0, 0.8, "outer")
        ],
        3.0);
}
