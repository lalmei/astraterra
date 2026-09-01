using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class CelestialMathTests
{
    [Fact]
    public void Seasonal_Angle_Advances_Full_Circle_Over_World_Year()
    {
        var start = CelestialMath.GetSeasonalAngle(dayOfYear: 0, dayFraction: 0, daysPerYear: 12);
        var end = CelestialMath.GetSeasonalAngle(dayOfYear: 12, dayFraction: 0, daysPerYear: 12);
        Assert.Equal(360.0, end - start, 6);
    }

    [Fact]
    public void LocalSiderealAngle_Advances_With_Time_Of_Day()
    {
        var start = CelestialMath.GetLocalSiderealAngle(dayOfYear: 0, dayFraction: 0, daysPerYear: 24);
        var afterTwentyHours = CelestialMath.GetLocalSiderealAngle(dayOfYear: 0, dayFraction: 20.0 / 24.0, daysPerYear: 24);

        Assert.Equal(312.5, afterTwentyHours - start, 6);
    }

    [Fact]
    public void LocalSiderealAngle_Adds_Seasonal_Drift_Per_World_Day()
    {
        var start = CelestialMath.GetLocalSiderealAngle(dayOfYear: 0, dayFraction: 0, daysPerYear: 24);
        var nextSolarMidnight = CelestialMath.GetLocalSiderealAngle(dayOfYear: 1, dayFraction: 0, daysPerYear: 24);

        Assert.Equal(15.0, nextSolarMidnight - start, 6);
    }

    [Fact]
    public void VanillaAlignedSidereal_UsesNoonAsSolarReference()
    {
        // Measured from a world whose year starts at the equinox, so the daily terms stand alone.
        var midnight = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 0, daysPerYear: 24, hoursPerDay: 24, equinoxDayOfYear: 0);
        var noon = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 0.5, daysPerYear: 24, hoursPerDay: 24, equinoxDayOfYear: 0);

        Assert.Equal(180.0, midnight, 6);
        Assert.Equal(7.5, noon, 6);
    }

    [Fact]
    public void VanillaAlignedSidereal_AppliesLongitudeOffset()
    {
        var primeMeridian = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 0.5, daysPerYear: 24, hoursPerDay: 24);
        var eastNinety = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 0.5, daysPerYear: 24, hoursPerDay: 24, longitudeDegrees: 90);

        Assert.Equal(90.0, CelestialMath.NormalizeDegrees(eastNinety - primeMeridian), 6);
    }

    [Fact]
    public void VanillaAlignedSidereal_AdvancesWithTimeOfDay()
    {
        const int daysPerYear = 360;
        const double hoursPerDay = 24;
        var midnight = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 100, daysPerYear: daysPerYear, hoursPerDay: hoursPerDay);
        var threeHoursLater = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays: 100 + (3.0 / hoursPerDay),
            daysPerYear: daysPerYear,
            hoursPerDay: hoursPerDay);

        var advance = SignedDelta(threeHoursLater - midnight);

        Assert.True(advance > 0, $"Sidereal time must advance as the day goes on, but moved {advance:0.###} deg.");
        Assert.Equal(3.0 * (15.0 + (360.0 / (daysPerYear * hoursPerDay))), advance, 6);
    }

    [Fact]
    public void Stars_Travel_East_To_West_Across_The_Night()
    {
        const int daysPerYear = 360;
        const double hoursPerDay = 24;
        const double transitDay = 100;
        const double latitudeDeg = 45;

        // Pick a star that sits exactly on the meridian at midnight of the sample day.
        var rightAscensionDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(transitDay, daysPerYear, hoursPerDay);

        var beforeTransit = SampleAzimuth(-3);
        var atTransit = SampleAzimuth(0);
        var afterTransit = SampleAzimuth(3);

        // Azimuth is measured clockwise from north: east is 90, south 180, west 270.
        Assert.InRange(beforeTransit, 90.0, 180.0);
        Assert.Equal(180.0, atTransit, 6);
        Assert.InRange(afterTransit, 180.0, 270.0);

        double SampleAzimuth(double hourOffset)
        {
            var localSiderealDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
                transitDay + (hourOffset / hoursPerDay),
                daysPerYear,
                hoursPerDay);
            var coordinates = CelestialMath.GetHorizontalCoordinates(
                rightAscensionDeg,
                declinationDeg: 0,
                latitudeDeg,
                localSiderealDeg);
            Assert.True(coordinates.AltitudeDeg > 0, "Sample must stay above the horizon.");
            return coordinates.AzimuthDeg;
        }
    }

    [Fact]
    public void SolarLongitude_Is_The_Seasonal_Term_Of_The_Sidereal_Angle()
    {
        const int daysPerYear = 360;
        const double hoursPerDay = 24;

        // At local noon the sidereal angle is exactly the seasonal term, which is what makes that
        // term stand in for solar longitude. Sampled across the year rather than at one date.
        for (var day = 0; day < daysPerYear; day += 7)
        {
            var noon = day + 0.5;
            var sidereal = CelestialMath.GetVanillaAlignedLocalSiderealAngle(noon, daysPerYear, hoursPerDay);
            var solarLongitude = CelestialMath.GetSolarLongitudeDegrees(noon, daysPerYear);

            Assert.Equal(CelestialMath.NormalizeDegrees(solarLongitude), sidereal, 9);
        }
    }

    [Theory]
    [InlineData(12)]
    [InlineData(108)]
    [InlineData(360)]
    public void SolarLongitude_Runs_A_Full_Turn_Over_A_World_Year_Whatever_Its_Length(int daysPerYear)
    {
        var equinox = CelestialMath.GetEquinoxDayOfYear(daysPerYear);

        Assert.Equal(0.0, CelestialMath.GetSolarLongitudeDegrees(equinox, daysPerYear), 9);
        Assert.Equal(90.0, CelestialMath.GetSolarLongitudeDegrees(equinox + (daysPerYear * 0.25), daysPerYear), 9);
        Assert.Equal(180.0, CelestialMath.GetSolarLongitudeDegrees(equinox + (daysPerYear * 0.5), daysPerYear), 9);
        Assert.Equal(270.0, CelestialMath.GetSolarLongitudeDegrees(equinox + (daysPerYear * 0.75), daysPerYear), 9);
        Assert.Equal(0.0, CelestialMath.GetSolarLongitudeDegrees(equinox + daysPerYear, daysPerYear), 9);

        // Same point in the year on the next lap, and the one before the world started.
        Assert.Equal(90.0, CelestialMath.GetSolarLongitudeDegrees(equinox + (daysPerYear * 3.25), daysPerYear), 9);
        Assert.Equal(270.0, CelestialMath.GetSolarLongitudeDegrees(equinox - (daysPerYear * 0.25), daysPerYear), 9);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(108)]
    [InlineData(360)]
    public void The_World_Year_Starts_In_Midwinter_Rather_Than_At_The_Equinox(int daysPerYear)
    {
        // Vintage Story runs its sun as -tilt * cos(2*pi*(yearRel + 10/365)), so day zero of a world
        // year is the first of January and the March equinox is about a fifth of the year later.
        Assert.Equal(daysPerYear * 0.2226, CelestialMath.GetEquinoxDayOfYear(daysPerYear), 2);
        Assert.Equal(279.86, CelestialMath.GetSolarLongitudeDegrees(0, daysPerYear), 2);
        Assert.Equal(90.0, CelestialMath.GetSolarLongitudeDegrees(daysPerYear * 0.4726, daysPerYear), 1);
    }

    [Fact]
    public void Betelgeuse_Comes_To_The_Meridian_At_Midnight_In_December()
    {
        // The regression this pins: with the year anchored at day zero, Betelgeuse transited at
        // midnight on day 269 -- late September on a world of twelve 30-day months, a season early.
        const int daysPerYear = 360;
        const double hoursPerDay = 24.0;
        const double betelgeuseRightAscensionDeg = 88.792935;

        // Midnight is a whole world day, so only those are sampled.
        var transitDay = -1;
        var closest = double.MaxValue;
        for (var day = 0; day < daysPerYear; day++)
        {
            var midnightSidereal = CelestialMath.GetVanillaAlignedLocalSiderealAngle(day, daysPerYear, hoursPerDay);
            var hourAngle = Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(betelgeuseRightAscensionDeg, midnightSidereal));
            if (hourAngle < closest)
            {
                closest = hourAngle;
                transitDay = day;
            }
        }

        // Day 349 of a 360-day year is the 19th of December, which is when it really happens.
        Assert.InRange(transitDay, 344, 354);
    }

    [Fact]
    public void SolarLongitude_Shifts_With_The_Equinox_Day()
    {
        const int daysPerYear = 360;

        Assert.Equal(0.0, CelestialMath.GetSolarLongitudeDegrees(90, daysPerYear, equinoxDayOfYear: 90), 9);
        Assert.Equal(90.0, CelestialMath.GetSolarLongitudeDegrees(180, daysPerYear, equinoxDayOfYear: 90), 9);
        Assert.Equal(270.0, CelestialMath.GetSolarLongitudeDegrees(0, daysPerYear, equinoxDayOfYear: 90), 9);
    }

    [Theory]
    [InlineData(10.0, 20.0, 10.0)]
    [InlineData(20.0, 10.0, -10.0)]
    [InlineData(359.0, 1.0, 2.0)]
    [InlineData(1.0, 359.0, -2.0)]
    [InlineData(350.0, 10.0, 20.0)]
    [InlineData(0.0, 180.0, 180.0)]
    [InlineData(0.0, 181.0, -179.0)]
    [InlineData(-10.0, 10.0, 20.0)]
    [InlineData(720.0, 5.0, 5.0)]
    public void ShortestAngularDistance_Takes_The_Short_Way_Round(double fromDegrees, double toDegrees, double expected)
    {
        Assert.Equal(expected, CelestialMath.ShortestAngularDistanceDegrees(fromDegrees, toDegrees), 9);
    }

    [Fact]
    public void ShortestAngularDistance_Never_Exceeds_Half_A_Turn()
    {
        for (var from = 0.0; from < 360.0; from += 7.0)
        {
            for (var to = 0.0; to < 360.0; to += 11.0)
            {
                var distance = CelestialMath.ShortestAngularDistanceDegrees(from, to);

                Assert.InRange(distance, -180.0, 180.0);
                Assert.Equal(
                    CelestialMath.NormalizeDegrees(to),
                    CelestialMath.NormalizeDegrees(from + distance),
                    9);
            }
        }
    }

    [Theory]
    [InlineData(0.0, 24.0, 0.0)]
    [InlineData(0.5, 24.0, 12.0)]
    [InlineData(10.25, 24.0, 6.0)]
    [InlineData(-0.25, 24.0, 18.0)]
    [InlineData(3.5, 16.0, 8.0)]
    public void LocalSolarTime_Wraps_Into_The_World_Day(double totalDays, double hoursPerDay, double expectedHours)
    {
        Assert.Equal(expectedHours, CelestialMath.GetLocalSolarTimeHours(totalDays, hoursPerDay), 6);
    }

    [Fact]
    public void LocalSolarTime_Rejects_A_Zero_Length_Day()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CelestialMath.GetLocalSolarTimeHours(totalDays: 1, hoursPerDay: 0));
    }

    [Theory]
    [InlineData(0.5f, 90.0, 0.75f)]
    [InlineData(0.9f, 90.0, 0.15f)]
    [InlineData(0.1f, -90.0, 0.85f)]
    [InlineData(0.25f, 360.0, 0.25f)]
    public void Longitude_Shifts_The_Solar_Delegate_By_A_Fraction_Of_One_Rotation(
        float dayRel,
        double longitudeDegrees,
        float expected)
    {
        Assert.Equal(expected, CelestialMath.ApplyLongitudeToDayRel(dayRel, longitudeDegrees), 5);
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0)]
    [InlineData(180.0, 180.0, 0.0)]
    [InlineData(90.0, 90.0, CelestialMath.MeanObliquityDeg)]
    [InlineData(270.0, 270.0, -CelestialMath.MeanObliquityDeg)]
    public void EclipticToEquatorial_Pins_The_Equinoxes_And_Solstices(
        double eclipticLongitudeDeg,
        double expectedRightAscensionDeg,
        double expectedDeclinationDeg)
    {
        var equatorial = CelestialMath.EclipticToEquatorial(eclipticLongitudeDeg, eclipticLatitudeDeg: 0);

        Assert.Equal(expectedRightAscensionDeg, equatorial.RightAscensionDeg, 6);
        Assert.Equal(expectedDeclinationDeg, equatorial.DeclinationDeg, 6);
    }

    [Fact]
    public void EclipticToEquatorial_Places_The_North_Ecliptic_Pole_In_Draco()
    {
        var pole = CelestialMath.EclipticToEquatorial(eclipticLongitudeDeg: 0, eclipticLatitudeDeg: 90);

        // 18h, +66.5 deg: the pole sits a full obliquity away from the celestial pole, which is the
        // sign check that catches a rotation applied the wrong way round.
        Assert.Equal(270.0, pole.RightAscensionDeg, 6);
        Assert.Equal(90.0 - CelestialMath.MeanObliquityDeg, pole.DeclinationDeg, 6);
    }

    [Fact]
    public void EclipticToEquatorial_Keeps_The_Ecliptic_Within_One_Obliquity_Of_The_Equator()
    {
        for (var longitudeDeg = 0.0; longitudeDeg < 360.0; longitudeDeg += 3.0)
        {
            var equatorial = CelestialMath.EclipticToEquatorial(longitudeDeg, eclipticLatitudeDeg: 0);

            Assert.InRange(
                equatorial.DeclinationDeg,
                -CelestialMath.MeanObliquityDeg - 1e-9,
                CelestialMath.MeanObliquityDeg + 1e-9);
        }
    }

    [Fact]
    public void EclipticToEquatorial_Is_The_Identity_On_A_World_With_No_Tilt()
    {
        var equatorial = CelestialMath.EclipticToEquatorial(
            eclipticLongitudeDeg: 137.5,
            eclipticLatitudeDeg: 12.25,
            obliquityDeg: 0);

        Assert.Equal(137.5, equatorial.RightAscensionDeg, 6);
        Assert.Equal(12.25, equatorial.DeclinationDeg, 6);
    }

    private static double SignedDelta(double degrees)
    {
        var normalized = CelestialMath.NormalizeDegrees(degrees);
        return normalized > 180.0 ? normalized - 360.0 : normalized;
    }
}
