using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// Checks the planets against facts about the real sky rather than against remembered coordinates.
/// An elongation limit or a retrograde loop cannot be satisfied by an ephemeris that is subtly wrong,
/// which is what makes them worth pinning: a sign error here would be exactly as invisible as the
/// sidereal one was.
/// </summary>
public sealed class PlanetEphemerisTests
{
    private const int DefaultDaysPerYear = 108;

    [Fact]
    public void The_World_Clock_Starts_At_The_March_Equinox()
    {
        // The world's own seasons read zero solar longitude at totalDays zero. If the planets were
        // anchored anywhere else, every one of them would sit in the wrong season.
        Assert.Equal(0.0, SunEclipticLongitudeDeg(totalDays: 0, DefaultDaysPerYear), 0);
    }

    [Fact]
    public void The_Sun_The_Planets_Are_Measured_From_Tracks_The_Sun_The_Seasons_Use()
    {
        for (var day = 0.0; day < DefaultDaysPerYear; day += 3.0)
        {
            var fromOrbit = SunEclipticLongitudeDeg(day, DefaultDaysPerYear);
            var fromSeasons = CelestialMath.GetSolarLongitudeDegrees(day, DefaultDaysPerYear);

            // Within the equation of centre, measured from its value at the equinox: the seasonal
            // model runs at a constant rate, while the world really speeds up at perihelion and
            // slows at aphelion, which is worth up to about 1.9 deg either way. Four degrees of
            // solar longitude is a quarter of an hour of transit timing, so nothing a player sees.
            Assert.InRange(Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(fromSeasons, fromOrbit)), 0.0, 4.0);
        }
    }

    [Theory]
    [InlineData("mercury", 28.5)]
    [InlineData("venus", 47.5)]
    public void An_Inner_Planet_Never_Strays_Far_From_The_Sun(string id, double maximumElongationDeg)
    {
        var catalog = LoadCatalog();
        var ephemeris = EphemerisFor(catalog, id, DefaultDaysPerYear);
        var greatest = 0.0;

        for (var day = 0.0; day < DefaultDaysPerYear * 3.0; day += 0.5)
        {
            greatest = Math.Max(greatest, ElongationDeg(catalog, ephemeris, day, DefaultDaysPerYear));
        }

        // Both bounds matter: too large means the geometry is wrong, too small means the planet is
        // not actually going round its orbit.
        Assert.InRange(greatest, maximumElongationDeg - 6.0, maximumElongationDeg);
    }

    [Fact]
    public void An_Outer_Planet_Comes_To_Opposition_Roughly_Once_A_World_Year()
    {
        var catalog = LoadCatalog();
        var jupiter = EphemerisFor(catalog, "jupiter", DefaultDaysPerYear);
        var greatest = 0.0;

        for (var day = 0.0; day < DefaultDaysPerYear * 1.2; day += 0.5)
        {
            greatest = Math.Max(greatest, ElongationDeg(catalog, jupiter, day, DefaultDaysPerYear));
        }

        Assert.True(greatest > 175.0, $"Jupiter never reached opposition; greatest elongation was {greatest:0.0} deg.");
    }

    [Fact]
    public void Mars_Turns_Back_On_Itself_Without_Anyone_Scripting_It()
    {
        var catalog = LoadCatalog();
        var mars = EphemerisFor(catalog, "mars", DefaultDaysPerYear);
        var retrogradeDays = 0.0;
        var previous = mars.PositionAt(0).RightAscensionDeg;

        // Retrograde loops recur about every 26 real months, so a little over two world years is
        // certain to contain one.
        for (var day = 0.25; day < DefaultDaysPerYear * 2.2; day += 0.25)
        {
            var current = mars.PositionAt(day).RightAscensionDeg;
            if (CelestialMath.ShortestAngularDistanceDegrees(previous, current) < 0)
            {
                retrogradeDays += 0.25;
            }

            previous = current;
        }

        // Roughly a tenth of the time, which is what a real observer charting Mars would find.
        Assert.InRange(retrogradeDays / DefaultDaysPerYear, 0.05, 0.25);
    }

    [Fact]
    public void Jupiter_Takes_About_Twelve_World_Years_Whatever_A_World_Year_Is()
    {
        foreach (var daysPerYear in new[] { 12, 108, 360 })
        {
            var catalog = LoadCatalog();
            var jupiter = EphemerisFor(catalog, "jupiter", daysPerYear);
            var startLongitude = HeliocentricLongitudeDeg(catalog, "jupiter", 0, daysPerYear);
            var returnedYears = 0.0;

            for (var year = 1.0; year < 20.0; year += 0.01)
            {
                var longitude = HeliocentricLongitudeDeg(catalog, "jupiter", year * daysPerYear, daysPerYear);
                if (Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(startLongitude, longitude)) < 1.0)
                {
                    returnedYears = year;
                    break;
                }
            }

            Assert.InRange(returnedYears, 11.7, 12.0);
            Assert.NotNull(jupiter);
        }
    }

    [Fact]
    public void The_Same_Point_In_A_World_Year_Shows_The_Same_Sky_On_Any_World()
    {
        var shortYear = EphemerisFor(LoadCatalog(), "saturn", daysPerYear: 12);
        var longYear = EphemerisFor(LoadCatalog(), "saturn", daysPerYear: 360);

        var onShortWorld = shortYear.PositionAt(12 * 3.25);
        var onLongWorld = longYear.PositionAt(360 * 3.25);

        Assert.Equal(onShortWorld.RightAscensionDeg, onLongWorld.RightAscensionDeg, 6);
        Assert.Equal(onShortWorld.DeclinationDeg, onLongWorld.DeclinationDeg, 6);
    }

    [Fact]
    public void Venus_Swings_Between_Brilliant_And_Merely_Bright()
    {
        var venus = EphemerisFor(LoadCatalog(), "venus", DefaultDaysPerYear);
        var brightest = double.MaxValue;
        var faintest = double.MinValue;

        for (var day = 0.0; day < DefaultDaysPerYear * 2.0; day += 0.5)
        {
            var magnitude = venus.MagnitudeAt(day);
            brightest = Math.Min(brightest, magnitude);
            faintest = Math.Max(faintest, magnitude);
        }

        // Real Venus runs about -4.9 to -3.8, and is the brightest thing in the sky after the sun
        // and moon at either end of that. The swing is the point: a player watching it over a season
        // sees it change without being told to look.
        Assert.True(brightest < -4.7, $"Venus only reached magnitude {brightest:0.00}.");
        Assert.True(faintest > -3.9, $"Venus never dimmed past magnitude {faintest:0.00}.");
        Assert.True(faintest - brightest > 1.0);
    }

    /// <summary>
    /// Brightest magnitudes as published, which the model should land near without being fitted to.
    /// Saturn's is its ringless value: the rings swing the real planet by up to half a magnitude as
    /// their tilt changes, and this model does not carry them.
    /// </summary>
    [Theory]
    [InlineData("mercury", -2.4)]
    [InlineData("venus", -4.9)]
    [InlineData("mars", -2.9)]
    [InlineData("jupiter", -2.9)]
    [InlineData("saturn", 0.4)]
    public void A_Planet_Reaches_About_The_Brightness_It_Really_Does(string id, double publishedBrightest)
    {
        var ephemeris = EphemerisFor(LoadCatalog(), id, DefaultDaysPerYear);
        var brightest = double.MaxValue;

        for (var day = 0.0; day < DefaultDaysPerYear * 24.0; day += 0.5)
        {
            brightest = Math.Min(brightest, ephemeris.MagnitudeAt(day));
        }

        // A magnitude zero point, an inverse-square term and a linear phase term will not match the
        // published cubics exactly. A third of a magnitude is far below what the sky can show, and
        // still tight enough to fail if the geometry is wrong.
        Assert.InRange(brightest, publishedBrightest - 0.35, publishedBrightest + 0.35);
    }

    /// <summary>
    /// Checks the whole chain — element rates, the world epoch, the world-year scaling and the
    /// obliquity rotation — against oppositions that really happened and whose circumstances are
    /// published. Nothing else here would catch an epoch that is a few days out: a wrong date still
    /// yields a plausible-looking sky, but not Mars at 0.373 au on the night it actually was.
    /// </summary>
    /// <param name="expectedDistanceAu">Published geocentric distance at the moment of opposition.</param>
    /// <param name="expectedDeclinationDeg">
    /// Published declination, which is the part the ecliptic-to-equatorial rotation decides. Distance
    /// alone would pass even with that rotation broken.
    /// </param>
    [Theory]
    [InlineData("mars", "2003-08-27T09:51:00Z", 0.3727, -15.8)]
    [InlineData("mars", "2018-07-27T05:07:00Z", 0.3860, -25.5)]
    [InlineData("mars", "2012-03-03T20:00:00Z", 0.6742, 10.3)]
    [InlineData("jupiter", "2010-09-21T11:00:00Z", 3.954, -2.3)]
    [InlineData("saturn", "2018-06-27T13:00:00Z", 9.045, -22.4)]
    public void A_Planet_Is_Where_It_Really_Was_At_A_Historic_Opposition(
        string id,
        string utc,
        double expectedDistanceAu,
        double expectedDeclinationDeg)
    {
        var catalog = LoadCatalog();
        var ephemeris = EphemerisFor(catalog, id, DefaultDaysPerYear);
        var totalDays = WorldDaysFor(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal));

        var distanceAu = ephemeris.GetGeocentricPosition(totalDays).Length;
        var declinationDeg = ephemeris.PositionAt(totalDays).DeclinationDeg;
        var elongationDeg = ElongationDeg(catalog, ephemeris, totalDays, DefaultDaysPerYear);

        Assert.InRange(distanceAu, expectedDistanceAu * 0.98, expectedDistanceAu * 1.02);
        Assert.InRange(declinationDeg, expectedDeclinationDeg - 2.0, expectedDeclinationDeg + 2.0);
        Assert.True(elongationDeg > 170.0, $"{id} was not at opposition: elongation {elongationDeg:0.0} deg.");
    }

    [Fact]
    public void Every_Planet_Stays_Near_The_Ecliptic()
    {
        var catalog = LoadCatalog();

        foreach (var planet in catalog.Planets)
        {
            var ephemeris = new PlanetEphemeris(planet, catalog.Observer, DefaultDaysPerYear);

            for (var day = 0.0; day < DefaultDaysPerYear * 2.0; day += 2.0)
            {
                // The zodiac is a band, and nothing in it reaches further from the equator than the
                // obliquity plus its own inclination.
                Assert.InRange(
                    ephemeris.PositionAt(day).DeclinationDeg,
                    -(CelestialMath.MeanObliquityDeg + 9.0),
                    CelestialMath.MeanObliquityDeg + 9.0);
                Assert.InRange(ephemeris.GetGeocentricPosition(day).EclipticLatitudeDeg, -9.0, 9.0);
            }
        }
    }

    [Fact]
    public void A_Planet_Rejects_A_World_With_No_Year()
    {
        var catalog = LoadCatalog();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PlanetEphemeris(catalog.Planets[0], catalog.Observer, daysPerYear: 0));
    }

    private static PlanetCatalog LoadCatalog()
        => PlanetCatalogLoader.Parse(File.ReadAllText("assets/astraterra/data/planets.v1.json"));

    /// <summary>
    /// World time for a real date, which is what <see cref="WorldEpoch"/> defines: a world clock of
    /// zero is the March 2000 equinox, and a world year is a Julian year.
    /// </summary>
    private static double WorldDaysFor(DateTime utc)
    {
        var epoch = new DateTime(2000, 3, 20, 7, 35, 0, DateTimeKind.Utc);
        return (utc - epoch).TotalDays / WorldEpoch.RealDaysPerWorldYear * DefaultDaysPerYear;
    }

    private static PlanetEphemeris EphemerisFor(PlanetCatalog catalog, string id, int daysPerYear)
        => new(catalog.Planets.Single(planet => planet.Id == id), catalog.Observer, daysPerYear);

    private static EclipticVector ObserverPosition(PlanetCatalog catalog, double totalDays, int daysPerYear)
        => KeplerianOrbit.GetHeliocentricPosition(
            catalog.Observer,
            WorldEpoch.GetJulianCenturies(totalDays, daysPerYear));

    private static double HeliocentricLongitudeDeg(PlanetCatalog catalog, string id, double totalDays, int daysPerYear)
        => KeplerianOrbit.GetHeliocentricPosition(
            catalog.Planets.Single(planet => planet.Id == id).Elements,
            WorldEpoch.GetJulianCenturies(totalDays, daysPerYear)).EclipticLongitudeDeg;

    /// <summary>Where the sun is seen from the observer's orbit: opposite the observer, by definition.</summary>
    private static double SunEclipticLongitudeDeg(double totalDays, int daysPerYear)
    {
        var observer = ObserverPosition(LoadCatalog(), totalDays, daysPerYear);
        return CelestialMath.NormalizeDegrees(observer.EclipticLongitudeDeg + 180.0);
    }

    /// <summary>Angle between the planet and the sun as the observer sees them.</summary>
    private static double ElongationDeg(
        PlanetCatalog catalog,
        PlanetEphemeris ephemeris,
        double totalDays,
        int daysPerYear)
    {
        var planet = ephemeris.GetGeocentricPosition(totalDays);
        var observer = ObserverPosition(catalog, totalDays, daysPerYear);
        var sun = new EclipticVector(-observer.X, -observer.Y, -observer.Z);
        var dot = (planet.X * sun.X) + (planet.Y * sun.Y) + (planet.Z * sun.Z);
        return Math.Acos(Math.Clamp(dot / (planet.Length * sun.Length), -1.0, 1.0)) * 180.0 / Math.PI;
    }
}
