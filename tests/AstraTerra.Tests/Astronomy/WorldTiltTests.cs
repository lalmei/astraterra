using AstraTerra.Astronomy;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// A world running on an axis of its own: that the sun it produces is still vanilla's sun when the
/// tilt is Earth's, that the tilt reaches what a tilt is supposed to reach, and that a locked moon's
/// eclipse cadence falls out of it rather than being authored.
/// </summary>
public sealed class WorldTiltTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Vintage Story's own solar delegate, transcribed from <c>SurvivalCoreSystem</c>. Pinned here
    /// so the substitution has something to be identical to: the whole approach rests on the claim
    /// that a world at Earth's tilt comes out of the replacement with the sun it already had, and
    /// that claim has to be checked rather than asserted in a comment.
    /// </summary>
    private static (double ZenithAngle, double AzimuthAngle) VanillaSolarSphericalCoords(
        double latitudeRel,
        float yearRel,
        float dayRel)
    {
        var latitude = latitudeRel * Math.PI / 2.0;
        var hourAngle = (float)Math.PI * 2f * (dayRel - 0.5f);
        var declination = (double)(0f - 0.40910518f) * Math.Cos((float)Math.PI * 2f * (yearRel + 0.02739726f));
        var sinLatitude = Math.Sin(latitude);
        var cosLatitude = Math.Cos(latitude);
        var sinDeclination = Math.Sin(declination);
        var sinAltitude = Math.Clamp(
            (sinLatitude * sinDeclination) + (cosLatitude * Math.Cos(declination) * Math.Cos(hourAngle)),
            -1.0,
            1.0);
        var cosAltitude = Math.Sqrt(1.0 - (sinAltitude * sinAltitude));
        var azimuth = (float)Math.Acos(
            Math.Clamp(
                ((sinLatitude * sinAltitude) - sinDeclination) / ((cosLatitude * cosAltitude) + 1.0000000116860974E-07),
                -1.0,
                1.0));
        azimuth = ((float)Math.PI + (Math.Sign(hourAngle) * azimuth)) % ((float)Math.PI * 2f);

        return (((float)Math.PI * 2f) - (float)Math.Acos(sinAltitude), ((float)Math.PI * 2f) - azimuth);
    }

    /// <summary>
    /// The load-bearing one. At the tilt vanilla itself runs on, the rebuilt sun stands exactly
    /// where the survival delegate would have put it -- every latitude, every hour, every season.
    /// If this ever drifts, installing this mod moves the sun on every world in the game, which is
    /// not a thing an astronomy mod is allowed to do quietly.
    /// </summary>
    [Fact]
    public void At_Earths_Tilt_The_Rebuilt_Sun_Is_Vanillas_Own()
    {
        foreach (var latitudeRel in new[] { -1.0, -0.62, -0.2, 0.0, 0.2, 0.62, 1.0 })
        {
            for (var day = 0; day < 12; day++)
            {
                var yearRel = day / 12f;
                for (var hour = 0; hour < 24; hour++)
                {
                    var dayRel = hour / 24f;
                    var expected = VanillaSolarSphericalCoords(latitudeRel, yearRel, dayRel);
                    var actual = WorldTilt.SolarSphericalCoords(
                        latitudeRel * Math.PI / 2.0,
                        yearRel,
                        dayRel,
                        WorldTilt.VanillaAxialTiltRad);

                    // A float's worth of slack: vanilla computes parts of this in single precision
                    // and this does not, so the two agree to about a millionth of a radian, which
                    // is a rounding difference and not a different sky.
                    Assert.Equal(expected.ZenithAngle, actual.ZenithAngle, tolerance: 1e-5);
                    Assert.Equal(expected.AzimuthAngle, actual.AzimuthAngle, tolerance: 1e-5);
                }
            }
        }
    }

    /// <summary>
    /// A world with no tilt has no seasons: the sun keeps the same track every day of the year.
    /// This is the Jupiter-analog end of the generated range, and the reason a moon out there is
    /// eclipsed almost daily.
    /// </summary>
    [Fact]
    public void An_Upright_World_Has_The_Same_Sun_All_Year()
    {
        var midsummer = WorldTilt.SolarSphericalCoords(0.7, yearRel: 0.5, dayRel: 0.5, tiltRad: 0.0);
        var midwinter = WorldTilt.SolarSphericalCoords(0.7, yearRel: 0.0, dayRel: 0.5, tiltRad: 0.0);

        Assert.Equal(midsummer.ZenithAngle, midwinter.ZenithAngle, 9);
    }

    /// <summary>
    /// The declination swings to exactly the tilt and no further, which is what makes the tilt the
    /// one number a season is made of.
    /// </summary>
    [Theory]
    [InlineData(3.1)]
    [InlineData(23.4392911)]
    [InlineData(26.7)]
    [InlineData(82.0)]
    public void The_Sun_Reaches_The_Tilt_And_Stops(double tiltDeg)
    {
        var tiltRad = WorldTilt.ToRadians(tiltDeg);
        var reached = 0.0;
        for (var step = 0; step < 3600; step++)
        {
            reached = Math.Max(
                reached,
                Math.Abs(WorldTilt.SolarDeclinationRad(step / 3600.0, tiltRad)));
        }

        Assert.Equal(tiltDeg, WorldTilt.ToDegrees(reached), 2);
    }

    /// <summary>
    /// Nothing is set by default, and clearing puts it back. A world nobody generated is Earth's.
    /// </summary>
    [Fact]
    public void An_Ungenerated_World_Keeps_Earths_Tilt()
    {
        WorldTilt.Reset();

        Assert.Equal(CelestialMath.MeanObliquityDeg, WorldTilt.CurrentDeg, Tolerance);
        Assert.False(WorldTilt.IsGenerated);
    }

    /// <summary>
    /// A tilt past the end of the scale is clamped rather than taken, and a value that is not a
    /// number is refused outright. Neither is a world anyone should be able to generate; both are
    /// what a slip in arithmetic somewhere upstream looks like by the time it arrives here.
    /// </summary>
    [Theory]
    [InlineData(-4.0, 0.0)]
    [InlineData(140.0, WorldTilt.MaxTiltDeg)]
    public void An_Impossible_Tilt_Is_Clamped(double asked, double kept)
    {
        try
        {
            WorldTilt.Set(asked);

            Assert.Equal(kept, WorldTilt.CurrentDeg, Tolerance);
        }
        finally
        {
            WorldTilt.Reset();
        }
    }

    [Fact]
    public void A_Tilt_That_Is_Not_A_Number_Is_Ignored()
    {
        try
        {
            WorldTilt.Set(double.NaN);

            Assert.Equal(CelestialMath.MeanObliquityDeg, WorldTilt.CurrentDeg, Tolerance);
        }
        finally
        {
            WorldTilt.Reset();
        }
    }

    /// <summary>
    /// The instruments follow the world rather than Earth. The bronze disc reads a latitude off how
    /// far the setting sun travels over a year, and that travel is set by the tilt of the world the
    /// disc is standing on -- so a disc holding Earth's number on a tipped world would return a
    /// latitude that is wrong, and wrong quietly.
    /// </summary>
    [Fact]
    public void The_Solar_Band_Widens_With_The_Worlds_Tilt()
    {
        try
        {
            WorldTilt.Set(8.0);
            var narrow = SolarBandPolicy.SwingDegAt(45.0);
            WorldTilt.Set(35.0);
            var wide = SolarBandPolicy.SwingDegAt(45.0);

            Assert.NotNull(narrow);
            Assert.NotNull(wide);
            Assert.True(
                wide!.Value > narrow!.Value,
                $"a more tipped world should swing its sunsets further: {wide} vs {narrow}");

            // And the disc still inverts its own relation, so a year of marks yields the latitude
            // that made them.
            Assert.Equal(45.0, SolarBandPolicy.LatitudeFromSwingDeg(wide.Value) ?? double.NaN, 6);
        }
        finally
        {
            WorldTilt.Reset();
        }
    }

    /// <summary>
    /// The sun the sky model computes follows the world's tilt too, because the sun a sextant
    /// measures and the sun a player sees have to be one object.
    /// </summary>
    [Fact]
    public void The_Modelled_Sun_Follows_The_Worlds_Tilt()
    {
        const int daysPerYear = 360;
        var solsticeDay = daysPerYear * (CelestialMath.SpringEquinoxYearFraction + 0.25);

        try
        {
            WorldTilt.Set(6.0);
            var upright = CelestialMath
                .GetVanillaAlignedSolarEquatorialCoordinates(solsticeDay, daysPerYear)
                .DeclinationDeg;
            WorldTilt.Set(40.0);
            var tipped = CelestialMath
                .GetVanillaAlignedSolarEquatorialCoordinates(solsticeDay, daysPerYear)
                .DeclinationDeg;

            Assert.Equal(6.0, upright, 3);
            Assert.Equal(40.0, tipped, 3);
        }
        finally
        {
            WorldTilt.Reset();
        }
    }

    /// <summary>
    /// The payoff. A giant on the moon's own celestial equator is crossed by the sun once every
    /// day; whether that crossing is an eclipse depends only on how far the sun's declination has
    /// got from the equator, and that is the world's tilt. An upright giant eclipses its moon
    /// almost daily, the way Jupiter does Io. A tipped one gives eclipse seasons instead, the way
    /// Saturn does Titan -- and the same generator produces both, from one number it was already
    /// drawing for the rings.
    /// </summary>
    [Theory]
    [InlineData(3.1, 340, 360)]
    [InlineData(26.7, 40, 140)]
    [InlineData(82.0, 5, 60)]
    public void The_Giants_Eclipse_Cadence_Falls_Out_Of_The_Worlds_Tilt(
        double tiltDeg,
        int fewestTotalDays,
        int mostTotalDays)
    {
        const int daysPerYear = 360;

        // A giant nineteen degrees wide is an ordinary generated one -- Jupiter from Io is 19.5.
        var giant = new NearBodyLightSource(
            AngularDiameterDeg: 19.5,
            HourAngleDeg: 40.0,
            DeclinationDeg: 0.0,
            Albedo: 0.52);

        var totalDays = 0;
        for (var day = 0; day < daysPerYear; day++)
        {
            var deepest = 0.0;
            for (var step = 0; step < 48; step++)
            {
                var moment = day + (step / 48.0);
                deepest = Math.Max(deepest, Obscuration(giant, moment, daysPerYear, hoursPerDay: 24.0, tiltDeg));
            }

            if (deepest > 0.999)
            {
                totalDays++;
            }
        }

        Assert.InRange(totalDays, fewestTotalDays, mostTotalDays);
    }

    /// <summary>
    /// Daily is not the same as constant. Even on the world that gets an eclipse every single day,
    /// the giant is a disc the sun goes behind and comes out of: the eclipse is a couple of hours,
    /// not a permanent dusk.
    /// </summary>
    [Fact]
    public void Even_A_Daily_Eclipse_Is_Over_By_Afternoon()
    {
        const int daysPerYear = 360;
        var giant = new NearBodyLightSource(19.5, 40.0, 0.0, 0.52);
        var eclipsedSamples = 0;
        const int samples = 240;

        for (var step = 0; step < samples; step++)
        {
            var moment = 100.0 + (step / (double)samples);
            if (Obscuration(giant, moment, daysPerYear, hoursPerDay: 24.0, tiltDeg: 3.1) > 0.0)
            {
                eclipsedSamples++;
            }
        }

        Assert.InRange(eclipsedSamples, 1, samples / 4);
    }

    /// <summary>
    /// A moon of a world on its side goes long stretches with no eclipse at all, because the sun
    /// spends most of the year far off the equator the giant sits on.
    /// </summary>
    [Fact]
    public void A_World_On_Its_Side_Has_Eclipse_Seasons_And_Long_Gaps()
    {
        const int daysPerYear = 360;
        var giant = new NearBodyLightSource(19.5, 40.0, 0.0, 0.52);
        var clearRun = 0;
        var longestClearRun = 0;

        for (var day = 0; day < daysPerYear; day++)
        {
            var deepest = 0.0;
            for (var step = 0; step < 48; step++)
            {
                deepest = Math.Max(
                    deepest,
                    Obscuration(giant, day + (step / 48.0), daysPerYear, hoursPerDay: 24.0, tiltDeg: 82.0));
            }

            clearRun = deepest > 0.0 ? 0 : clearRun + 1;
            longestClearRun = Math.Max(longestClearRun, clearRun);
        }

        Assert.True(
            longestClearRun > 60,
            $"a world on its side should go a season without an eclipse; longest clear run was {longestClearRun} days");
    }

    /// <summary>
    /// How much of the sun the giant is covering at a moment, with the sun placed by this world's
    /// own tilt exactly as the game's solar delegate would place it.
    /// </summary>
    private static double Obscuration(
        NearBodyLightSource giant,
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        double tiltDeg)
    {
        var yearRel = (totalDays % daysPerYear) / daysPerYear;
        var dayRel = totalDays % 1.0;
        var (zenith, azimuth) = WorldTilt.SolarSphericalCoords(
            latitudeRad: 0.0,
            yearRel,
            dayRel,
            WorldTilt.ToRadians(tiltDeg));

        // The calendar's own conversion from those two angles to a direction in the world.
        var sinZenith = Math.Sin(zenith);
        var sun = new SkyDirection(
            sinZenith * Math.Sin(azimuth),
            Math.Cos(zenith),
            sinZenith * Math.Cos(azimuth));

        return NearBodyLightController
            .IlluminationFor(giant, latitudeDeg: 0.0, longitudeDeg: 0.0, totalDays, daysPerYear, hoursPerDay, sun)
            .SolarObscuration;
    }
}
