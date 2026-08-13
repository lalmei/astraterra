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

    private static PlanetCatalog LoadCatalog()
        => PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));
}
