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
        var midnight = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 0, daysPerYear: 24, hoursPerDay: 24);
        var noon = CelestialMath.GetVanillaAlignedLocalSiderealAngle(totalDays: 0.5, daysPerYear: 24, hoursPerDay: 24);

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

    private static double SignedDelta(double degrees)
    {
        var normalized = CelestialMath.NormalizeDegrees(degrees);
        return normalized > 180.0 ? normalized - 360.0 : normalized;
    }
}
