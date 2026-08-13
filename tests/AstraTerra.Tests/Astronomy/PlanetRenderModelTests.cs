using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class PlanetRenderModelTests
{
    private const int DefaultDaysPerYear = 108;
    private const double DefaultHoursPerDay = 24.0;

    [Fact]
    public void A_Planet_Is_Placed_Exactly_Where_A_Star_At_Its_Position_Would_Be()
    {
        var planet = PlanetAt(new EquatorialCoordinates(45.0, 12.0), visualMagnitude: -2.0);

        var rendered = PlanetRenderModel.Project(
            planet.Entry,
            planet.Ephemeris,
            totalDays: 0,
            latitudeDeg: 30,
            localSiderealDeg: 45,
            brightnessBias: 1);
        var star = StarRenderModel.Project(
            new StarCatalogEntry(1, 45.0, 12.0, -2.0, null, false),
            latitudeDeg: 30,
            localSiderealDeg: 45,
            brightnessBias: 1);

        Assert.Equal(star!.Body, rendered!.Body);
    }

    [Fact]
    public void A_Planet_Below_The_Horizon_Is_Not_Drawn()
    {
        var planet = PlanetAt(new EquatorialCoordinates(180.0, 0.0), visualMagnitude: -2.0);

        var rendered = PlanetRenderModel.Project(
            planet.Entry,
            planet.Ephemeris,
            totalDays: 0,
            latitudeDeg: 0,
            localSiderealDeg: 0,
            brightnessBias: 1);

        Assert.Null(rendered);
    }

    [Fact]
    public void A_Planet_Carries_Its_Name_And_Tint_Through_Projection()
    {
        var planet = PlanetAt(new EquatorialCoordinates(0.0, 0.0), visualMagnitude: -1.0);

        var rendered = PlanetRenderModel.Project(
            planet.Entry,
            planet.Ephemeris,
            totalDays: 0,
            latitudeDeg: 0,
            localSiderealDeg: 0,
            brightnessBias: 1);

        Assert.Equal("mars", rendered!.Id);
        Assert.Equal("Mars", rendered.DisplayName);
        Assert.Equal(1.0f, rendered.TintR);
        Assert.Equal(0.55f, rendered.TintG);
        Assert.Equal(0.38f, rendered.TintB);
    }

    [Fact]
    public void Brilliance_Keeps_Separating_Planets_The_Star_Curve_Has_Already_Saturated()
    {
        var venus = SkyProjection.GetBrightnessFromMagnitude(-4.6);
        var jupiter = SkyProjection.GetBrightnessFromMagnitude(-2.2);

        // The shared star curve cannot tell these apart at all, which is what brilliance is for.
        Assert.Equal(venus, jupiter);
        Assert.True(PlanetRenderModel.GetBrilliance(-4.6) > PlanetRenderModel.GetBrilliance(-2.2));
        Assert.True(PlanetRenderModel.GetBrilliance(-2.2) > PlanetRenderModel.GetBrilliance(0.6));
    }

    [Theory]
    [InlineData(-6.0, 1.0)]
    [InlineData(-4.5, 1.0)]
    [InlineData(2.0, 0.0)]
    [InlineData(5.0, 0.0)]
    public void Brilliance_Stays_Inside_Its_Range(double visualMagnitude, double expected)
    {
        Assert.Equal(expected, PlanetRenderModel.GetBrilliance(visualMagnitude), 9);
    }

    [Fact]
    public void The_Shipped_Planets_Are_Drawn_Brightest_First()
    {
        var model = new PlanetRenderModel(LoadCatalog(), DefaultDaysPerYear, DefaultHoursPerDay);

        // Somewhere in the world year every planet is up at once for an equatorial observer, so
        // sweep sidereal angles until several are visible together.
        var visible = Enumerable.Range(0, 24)
            .Select(step => model.ProjectVisiblePlanets(
                totalDays: 40,
                latitudeDeg: 0,
                localSiderealDeg: step * 15.0,
                brightnessBias: 1))
            .OrderByDescending(planets => planets.Count)
            .First();

        Assert.True(visible.Count >= 2, "Expected at least two planets above the horizon at some hour.");
        Assert.Equal(visible.OrderByDescending(planet => planet.Brightness).Select(planet => planet.Id), visible.Select(planet => planet.Id));
    }

    [Fact]
    public void Every_Shipped_Planet_Comes_Round_To_Being_Visible()
    {
        var model = new PlanetRenderModel(LoadCatalog(), DefaultDaysPerYear, DefaultHoursPerDay);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var day = 0.0; day < DefaultDaysPerYear * 2.0; day += 1.0)
        {
            for (var siderealDeg = 0.0; siderealDeg < 360.0; siderealDeg += 30.0)
            {
                foreach (var planet in model.ProjectVisiblePlanets(day, latitudeDeg: 20, localSiderealDeg: siderealDeg, brightnessBias: 1))
                {
                    seen.Add(planet.Id);
                }
            }
        }

        Assert.Equal(5, seen.Count);
    }

    [Fact]
    public void A_Planet_Model_Rejects_A_World_With_No_Year_Or_No_Day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlanetRenderModel(LoadCatalog(), daysPerYear: 0, hoursPerDay: 24));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlanetRenderModel(LoadCatalog(), daysPerYear: 108, hoursPerDay: 0));
    }

    private static PlanetCatalog LoadCatalog()
        => PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));

    private static (PlanetEntry Entry, ISkyEphemeris Ephemeris) PlanetAt(
        EquatorialCoordinates coordinates,
        double visualMagnitude)
    {
        var entry = LoadCatalog().Planets.Single(planet => planet.Id == "mars");
        return (entry, new FixedEphemeris(coordinates, visualMagnitude));
    }
}
