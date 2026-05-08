using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class ConstellationSeasonServiceTests
{
    [Fact]
    public void Season_Service_Returns_Window_And_State()
    {
        var info = ConstellationSeasonService.Describe(
            averageRightAscensionDeg: 90,
            averageDeclinationDeg: 20,
            latitudeDeg: 45,
            dayOfYear: 90,
            hourAfterSunset: 2
        );

        Assert.False(string.IsNullOrWhiteSpace(info.MonthWindow));
        Assert.False(string.IsNullOrWhiteSpace(info.SeasonSummary));
        Assert.False(string.IsNullOrWhiteSpace(info.State));
    }

    [Theory]
    [InlineData(0, 0, "culminating")]
    [InlineData(45, 20, "culminating")]
    [InlineData(-45, -20, "culminating")]
    public void Season_Service_Handles_Equator_And_Both_Hemispheres(double latitudeDeg, double declinationDeg, string expectedState)
    {
        var info = ConstellationSeasonService.Describe(
            averageRightAscensionDeg: 180,
            averageDeclinationDeg: declinationDeg,
            latitudeDeg: latitudeDeg,
            dayOfYear: 0,
            hourAfterSunset: 0);

        Assert.Equal(expectedState, info.State);
    }

    [Fact]
    public void Season_Service_Reports_Below_Horizon_Near_Pole()
    {
        var info = ConstellationSeasonService.Describe(
            averageRightAscensionDeg: 0,
            averageDeclinationDeg: -60,
            latitudeDeg: 80,
            dayOfYear: 0,
            hourAfterSunset: 0);

        Assert.Equal("below horizon", info.State);
    }
}
