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

        Assert.Equal(270.0, CelestialMath.NormalizeDegrees(eastNinety - primeMeridian), 6);
    }
}
