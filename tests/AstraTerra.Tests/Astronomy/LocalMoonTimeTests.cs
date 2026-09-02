using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// Vintage Story's moon takes no X and has no replaceable delegate, so the only way it can follow
/// the longitude-aware sun is to be read at the observer's own time instead of the world's.
/// </summary>
public sealed class LocalMoonTimeTests
{
    [Fact]
    public void An_Eastward_Observer_Reads_The_Moon_Later_In_The_Day()
    {
        var moonDays = LocalMoonTime.MoonTotalDays(
            totalDays: 10.25,
            longitudeDegrees: 90,
            astraTerraDrawsTheMoon: true);

        // A quarter of the world around is a quarter of a world day, the same offset the sun's
        // wrapper applies to dayRel.
        Assert.Equal(10.5, moonDays, 9);
    }

    [Fact]
    public void A_Westward_Observer_Reads_It_Earlier()
    {
        var moonDays = LocalMoonTime.MoonTotalDays(
            totalDays: 10.25,
            longitudeDegrees: -180,
            astraTerraDrawsTheMoon: true);

        Assert.Equal(9.75, moonDays, 9);
    }

    [Fact]
    public void The_Prime_Meridian_Changes_Nothing()
    {
        Assert.Equal(
            10.25,
            LocalMoonTime.MoonTotalDays(totalDays: 10.25, longitudeDegrees: 0, astraTerraDrawsTheMoon: true),
            9);
    }

    /// <summary>
    /// While Vintage Story is drawing its own disc, the mod measures the body the game actually put
    /// in the sky rather than one it would have preferred.
    /// </summary>
    [Fact]
    public void A_Vanilla_Moon_Is_Left_On_The_Worlds_Own_Time()
    {
        Assert.Equal(
            10.25,
            LocalMoonTime.MoonTotalDays(totalDays: 10.25, longitudeDegrees: 90, astraTerraDrawsTheMoon: false),
            9);
    }
}
