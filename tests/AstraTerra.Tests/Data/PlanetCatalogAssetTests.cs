using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class PlanetCatalogAssetTests
{
    [Fact]
    public void PlanetCatalog_Ships_The_Five_Naked_Eye_Wanderers()
    {
        var catalog = LoadCatalog();

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(
            ["mercury", "venus", "mars", "jupiter", "saturn"],
            catalog.Planets.Select(planet => planet.Id));
        Assert.All(catalog.Planets, planet => Assert.False(string.IsNullOrWhiteSpace(planet.DisplayName)));
    }

    /// <summary>
    /// Kepler's third law ties a body's semi-major axis to how fast its mean longitude advances, and
    /// the catalog carries both as independent numbers. A mistyped digit in either breaks the
    /// relation by far more than the tolerance here, which makes this the check that a hand-copied
    /// element table most needs.
    /// </summary>
    [Fact]
    public void Every_Orbit_Obeys_Kepler_Third_Law()
    {
        var catalog = LoadCatalog();
        var orbits = catalog.Planets
            .Select(planet => (planet.Id, planet.Elements))
            .Append(("observer", catalog.Observer));

        foreach (var (id, elements) in orbits)
        {
            var yearsFromMeanMotion = 36_000.0 / elements.MeanLongitudeRateDegPerCentury;
            var yearsFromSemiMajorAxis = Math.Pow(elements.SemiMajorAxisAu, 1.5);

            Assert.True(
                Math.Abs((yearsFromMeanMotion / yearsFromSemiMajorAxis) - 1.0) < 0.005,
                $"{id} is not on a Keplerian orbit: {yearsFromMeanMotion:0.0000} years from its mean motion, " +
                $"{yearsFromSemiMajorAxis:0.0000} from its semi-major axis.");
        }
    }

    [Fact]
    public void Every_Orbit_Is_Ordered_And_Shaped_Like_The_Real_Solar_System()
    {
        var catalog = LoadCatalog();
        var semiMajorAxes = catalog.Planets.Select(planet => planet.Elements.SemiMajorAxisAu).ToList();

        // Mercury, Venus, then the observer's own orbit, then Mars, Jupiter, Saturn.
        Assert.Equal(semiMajorAxes.OrderBy(axis => axis), semiMajorAxes);
        Assert.True(semiMajorAxes[1] < catalog.Observer.SemiMajorAxisAu);
        Assert.True(semiMajorAxes[2] > catalog.Observer.SemiMajorAxisAu);

        Assert.All(
            catalog.Planets,
            planet =>
            {
                // Ellipses, not hyperbolas, and all within a few degrees of the ecliptic.
                Assert.InRange(planet.Elements.Eccentricity, 0.0, 0.3);
                Assert.InRange(Math.Abs(planet.Elements.InclinationDeg), 0.0, 8.0);
                Assert.InRange(planet.PhaseCoefficient, 0.0, 0.1);
                Assert.InRange(planet.AbsoluteMagnitude, -10.0, 1.0);
            });
    }

    [Fact]
    public void Every_Planet_Carries_A_Drawable_Tint()
    {
        var catalog = LoadCatalog();

        Assert.All(
            catalog.Planets,
            planet =>
            {
                Assert.InRange(planet.TintR, 0.0f, 1.0f);
                Assert.InRange(planet.TintG, 0.0f, 1.0f);
                Assert.InRange(planet.TintB, 0.0f, 1.0f);
            });

        // Mars is the one a player should be able to pick out by colour alone.
        var mars = catalog.Planets.Single(planet => planet.Id == "mars");
        Assert.True(mars.TintR > mars.TintG && mars.TintG > mars.TintB);
    }

    /// <summary>
    /// Every planet ships the faces a telescope needs, and every face has a picture behind it: a
    /// missing texture is silent in the eyepiece, indistinguishable from a planet that has not risen.
    /// </summary>
    [Fact]
    public void Every_Planet_Ships_A_Disc_With_Faces_Across_The_Phase()
    {
        var catalog = LoadCatalog();

        Assert.All(
            catalog.Planets,
            planet =>
            {
                var disc = planet.Disc;
                Assert.NotNull(disc);
                Assert.Equal([0.0, 90.0, 180.0], disc.Faces.Select(face => face.PhaseAngleDeg));
                Assert.True(disc.EquatorialDiameterKm > 1000.0, $"{planet.Id} has no globe to draw.");
                Assert.InRange(disc.ImageWidthInDiameters, 1.0, 3.0);
                Assert.All(disc.Faces, face => AssertTextureExists(face.TexturePath));
            });

        // Only Saturn's picture is wider than its globe, because only Saturn's rings need the room.
        Assert.Equal(
            ["saturn"],
            catalog.Planets.Where(planet => planet.Disc!.ImageWidthInDiameters > 1.0).Select(planet => planet.Id));
    }

    [Fact]
    public void The_Two_Giants_Carry_The_Moons_A_Telescope_Shows()
    {
        var catalog = LoadCatalog();
        var withMoons = catalog.Planets.Where(planet => planet.Moons is { Count: > 0 }).ToList();

        Assert.Equal(["jupiter", "saturn"], withMoons.Select(planet => planet.Id));
        Assert.Equal(
            ["io", "europa", "ganymede", "callisto"],
            catalog.Planets.Single(planet => planet.Id == "jupiter").Moons!.Select(moon => moon.Id));
        Assert.Equal(
            ["enceladus", "tethys", "dione", "rhea", "titan"],
            catalog.Planets.Single(planet => planet.Id == "saturn").Moons!.Select(moon => moon.Id));

        Assert.All(
            withMoons.SelectMany(planet => planet.Moons!),
            moon =>
            {
                Assert.False(string.IsNullOrWhiteSpace(moon.DisplayName));
                Assert.InRange(moon.Brightness, 0.1, 1.0);
                Assert.InRange(moon.MeanLongitudeDeg, 0.0, 360.0);
                AssertTextureExists(moon.TexturePath);
            });

        // Saturn's system is the one tipped far enough for its moons to open into an ellipse rather
        // than a line, which is the same tilt its rings are drawn at.
        Assert.InRange(catalog.Planets.Single(planet => planet.Id == "saturn").MoonPlaneTiltDeg, 20.0, 30.0);
        Assert.InRange(catalog.Planets.Single(planet => planet.Id == "jupiter").MoonPlaneTiltDeg, 0.0, 5.0);
    }

    /// <summary>
    /// The same check the planets get, one level down: a moon's period and its distance from its
    /// parent are independent numbers in the catalog, and Kepler ties them together. Within one
    /// system the constant is the parent's mass, so the ratio has to hold across every moon of it.
    /// </summary>
    [Fact]
    public void Every_Moon_Orbits_Its_Parent_On_A_Keplerian_Orbit()
    {
        var catalog = LoadCatalog();

        foreach (var planet in catalog.Planets.Where(planet => planet.Moons is { Count: > 0 }))
        {
            var moons = planet.Moons!;
            var reference = Math.Pow(moons[0].SemiMajorAxisKm, 3.0) / Math.Pow(moons[0].OrbitalPeriodRealDays, 2.0);

            Assert.Equal(
                moons.OrderBy(moon => moon.SemiMajorAxisKm).Select(moon => moon.Id),
                moons.Select(moon => moon.Id));

            foreach (var moon in moons)
            {
                var constant = Math.Pow(moon.SemiMajorAxisKm, 3.0) / Math.Pow(moon.OrbitalPeriodRealDays, 2.0);
                Assert.True(
                    Math.Abs((constant / reference) - 1.0) < 0.02,
                    $"{moon.Id} is not on a Keplerian orbit around {planet.Id}: {constant:0.###e+0} against {reference:0.###e+0}.");
            }
        }
    }

    private static void AssertTextureExists(string texturePath)
    {
        var file = Path.Combine(
            "assets/astraterra/textures",
            texturePath.Replace("astraterra:", string.Empty, StringComparison.Ordinal) + ".png");
        Assert.True(File.Exists(file), $"{texturePath} has no picture behind it at {file}.");
    }

    private static PlanetCatalog LoadCatalog()
        => PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));
}
