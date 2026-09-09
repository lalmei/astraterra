using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// What a parent giant does to the light on the ground beneath it, checked against the two things
/// the model claims: that a full giant is a real light, and that a giant on the sun's track
/// eclipses it in seasons rather than daily or never.
/// </summary>
public sealed class NearBodyLightTests
{
    /// <summary>Jupiter as Io sees it: 19.5 degrees wide, half its light sent back.</summary>
    private static NearBodyLightSource JupiterFromIo()
        => new(AngularDiameterDeg: 19.5, HourAngleDeg: 0.0, DeclinationDeg: 0.04, Albedo: 0.52);

    /// <summary>
    /// The reflectance model has to land on the moon we can check, or it cannot be trusted on the
    /// ones we cannot. Earth's full moon is about two and a half parts per million of daylight, and
    /// a one-line Lambert sphere should get within a factor of two of that.
    /// </summary>
    [Fact]
    public void The_Reflectance_Model_Reproduces_Earths_Full_Moon()
    {
        var moonFlux = 0.12 * Math.Pow(Math.Sin(0.26 * Math.PI / 180.0), 2.0) * (2.0 / 3.0);

        Assert.InRange(moonFlux, 1.0e-6, 3.0e-6);
        Assert.Equal(NearBodyLight.VanillaFullMoonLight, NearBodyLight.PlanetshineStrength(moonFlux), 2);
    }

    /// <summary>
    /// The headline claim. A locked moon's night under a full giant is not a moonlit night; it is
    /// its own kind of daylight, and the number has to say so -- comfortably above vanilla's full
    /// moon and comfortably below noon.
    /// </summary>
    [Fact]
    public void A_Full_Parent_Giant_Lights_The_Night_Far_Past_A_Full_Moon()
    {
        var giant = JupiterFromIo();
        var overhead = new SkyDirection(0.0, 1.0, 0.0);
        var sunBelow = new SkyDirection(0.0, -1.0, 0.0);

        var light = NearBodyLight.Illumination(giant, overhead, 90.0, sunBelow);

        Assert.Equal(0.0, light.SolarObscuration);
        Assert.True(
            light.PlanetshineStrength > NearBodyLight.VanillaFullMoonLight,
            $"a full giant should outshine a full moon, got {light.PlanetshineStrength}");
        Assert.InRange(light.PlanetshineStrength, 0.4, NearBodyLight.MaxPlanetshineLight);
    }

    /// <summary>
    /// Phase is the whole reason the giant is a night light rather than a permanent one. At local
    /// noon the sun is behind the giant, its lit face is turned away, and it adds nothing.
    /// </summary>
    [Fact]
    public void A_New_Parent_Giant_Adds_No_Light_At_All()
    {
        var giant = JupiterFromIo();
        var overhead = new SkyDirection(0.0, 1.0, 0.0);

        var light = NearBodyLight.Illumination(giant, overhead, 90.0, overhead);

        Assert.Equal(0.0, light.PlanetshineStrength, 6);
    }

    /// <summary>
    /// A giant below the horizon does not light the ground. An observer on the far side of a locked
    /// moon never sees theirs, and their nights should be the dark ones.
    /// </summary>
    [Fact]
    public void A_Parent_Giant_Below_The_Horizon_Lights_Nothing()
    {
        var giant = JupiterFromIo();
        var down = new SkyDirection(0.0, -1.0, 0.0);
        var sunUp = new SkyDirection(0.0, 1.0, 0.0);

        var light = NearBodyLight.Illumination(giant, down, -90.0, sunUp);

        Assert.Equal(0.0, light.PlanetshineStrength, 6);
    }

    /// <summary>
    /// The eclipse is total when the sun is behind the disc, absent when it is clear of it, and
    /// partial in between -- and the shoulder either side is wide, because the sun takes real time
    /// to cross an edge that is forty times its own width away from centre.
    /// </summary>
    [Fact]
    public void Obscuration_Runs_From_Total_Through_Partial_To_Clear()
    {
        const double giantDeg = 19.5;
        var sunDeg = NearBodyLight.SunAngularDiameterDeg;

        Assert.Equal(1.0, NearBodyLight.Obscuration(0.0, giantDeg, sunDeg));
        Assert.Equal(1.0, NearBodyLight.Obscuration(9.0, giantDeg, sunDeg));
        Assert.Equal(0.0, NearBodyLight.Obscuration(10.02, giantDeg, sunDeg));

        // Centres exactly one giant-radius apart puts the giant's limb across the sun's centre,
        // which is half the sun covered whatever the two sizes are.
        Assert.Equal(0.5, NearBodyLight.Obscuration(giantDeg * 0.5, giantDeg, sunDeg), 6);

        var partial = NearBodyLight.Obscuration(9.85, giantDeg, sunDeg);
        Assert.InRange(partial, 0.0, 1.0);
    }

    /// <summary>
    /// The point of moving the giant onto the celestial equator. The sun's declination swings
    /// through the year, so the two meet twice -- around the equinoxes -- and miss at the
    /// solstices. Neither daily nor never.
    /// </summary>
    [Fact]
    public void An_Equatorial_Giant_Is_Eclipsed_In_Seasons_Not_Daily()
    {
        var giant = JupiterFromIo();
        var eclipsedDays = 0;
        var clearDays = 0;
        const int daysPerYear = 360;

        for (var day = 0; day < daysPerYear; day++)
        {
            // The sun comes round to the giant's hour angle once a day; what changes across the
            // year is only how high it is when it gets there.
            var sunDeclinationDeg = 23.5 * Math.Sin(2.0 * Math.PI * day / daysPerYear);
            var separation = Math.Abs(sunDeclinationDeg - giant.DeclinationDeg);
            var obscuration = NearBodyLight.Obscuration(
                separation,
                giant.AngularDiameterDeg,
                NearBodyLight.SunAngularDiameterDeg);
            if (obscuration > 0.0)
            {
                eclipsedDays++;
            }
            else
            {
                clearDays++;
            }
        }

        Assert.True(eclipsedDays > 0, "an equatorial giant must eclipse the sun at some point in the year");
        Assert.True(clearDays > 0, "an equatorial giant must not eclipse the sun every day of the year");

        // Two seasons, not one long one and not scattered days: the eclipsed stretch either side of
        // each equinox is contiguous.
        Assert.Equal(2, SeasonCount(daysPerYear, giant));
    }

    /// <summary>
    /// The old authored declination is the control, and it says something sharper than "no
    /// eclipses". A giant parked 26 degrees off the equator is still inside the sun's own 23.5
    /// degree swing plus its own width, so it was never actually clear of the sun -- but the sun
    /// only reaches up to it at one solstice and runs 49 degrees away at the other, so it got one
    /// lopsided eclipse season a year. Putting the giant on the equator is what makes the two
    /// crossings symmetric, which is what an eclipse season is supposed to be.
    /// </summary>
    [Fact]
    public void An_Offset_Giant_Gets_One_Season_A_Year_And_An_Equatorial_One_Gets_Two()
    {
        var offset = new NearBodyLightSource(
            AngularDiameterDeg: 8.0,
            HourAngleDeg: 0.0,
            DeclinationDeg: 26.0,
            Albedo: 0.52);

        Assert.Equal(1, SeasonCount(360, offset));
        Assert.Equal(2, SeasonCount(360, JupiterFromIo()));
    }

    /// <summary>
    /// Nothing outside a moon world is touched: no giant, no change, on any input.
    /// </summary>
    [Fact]
    public void A_World_Without_A_Giant_Gets_No_Illumination()
    {
        var light = NearBodyLight.Illumination(
            source: null,
            new SkyDirection(0.0, 1.0, 0.0),
            90.0,
            new SkyDirection(0.0, -1.0, 0.0));

        Assert.Equal(NearBodyIllumination.None, light);
    }

    /// <summary>
    /// However bright the giant, the night stays a night. The curve does this on its own -- even a
    /// physically impossible giant, ninety degrees wide and reflecting everything, lands well under
    /// the ceiling -- which is the point: the ceiling is a guard against bad arithmetic, not the
    /// thing keeping generated worlds honest.
    /// </summary>
    [Fact]
    public void No_Real_Giant_Comes_Near_The_Ceiling()
    {
        var enormous = new NearBodyLightSource(
            AngularDiameterDeg: 90.0,
            HourAngleDeg: 0.0,
            DeclinationDeg: 0.0,
            Albedo: 1.0);

        var light = NearBodyLight.Illumination(
            enormous,
            new SkyDirection(0.0, 1.0, 0.0),
            90.0,
            new SkyDirection(0.0, -1.0, 0.0));

        Assert.InRange(light.PlanetshineStrength, 0.45, 0.6);
        Assert.True(light.PlanetshineStrength < NearBodyLight.MaxPlanetshineLight);
    }

    /// <summary>And nonsense input, which is what the ceiling is actually for, is clamped.</summary>
    [Fact]
    public void Planetshine_Cannot_Exceed_Its_Ceiling()
    {
        Assert.Equal(NearBodyLight.MaxPlanetshineLight, NearBodyLight.PlanetshineStrength(1.0e6), 6);
        Assert.Equal(0.0, NearBodyLight.PlanetshineStrength(double.NaN));
        Assert.Equal(0.0, NearBodyLight.PlanetshineStrength(-1.0));
    }

    /// <summary>How many separate stretches of the year the sun spends behind the giant.</summary>
    private static int SeasonCount(int daysPerYear, NearBodyLightSource giant)
    {
        var seasons = 0;
        var wasEclipsed = false;
        var firstEclipsed = false;
        var lastEclipsed = false;

        for (var day = 0; day < daysPerYear; day++)
        {
            var sunDeclinationDeg = 23.5 * Math.Sin(2.0 * Math.PI * day / daysPerYear);
            var eclipsed = NearBodyLight.Obscuration(
                Math.Abs(sunDeclinationDeg - giant.DeclinationDeg),
                giant.AngularDiameterDeg,
                NearBodyLight.SunAngularDiameterDeg) > 0.0;
            if (eclipsed && !wasEclipsed)
            {
                seasons++;
            }

            if (day == 0)
            {
                firstEclipsed = eclipsed;
            }

            lastEclipsed = eclipsed;
            wasEclipsed = eclipsed;
        }

        // The year is a circle: a season straddling day zero was counted twice.
        return firstEclipsed && lastEclipsed ? Math.Max(1, seasons - 1) : seasons;
    }
}
