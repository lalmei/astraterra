using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class SkyGridModeTests
{
    [Theory]
    [InlineData("none", SkyGridMode.None)]
    [InlineData("off", SkyGridMode.None)]
    [InlineData("horizontal", SkyGridMode.Horizontal)]
    [InlineData("alt-az", SkyGridMode.Horizontal)]
    [InlineData("EQUATORIAL", SkyGridMode.Equatorial)]
    [InlineData("ra-dec", SkyGridMode.Equatorial)]
    [InlineData(" both ", SkyGridMode.Both)]
    public void TryParse_Accepts_Supported_Grid_Values(string value, SkyGridMode expected)
    {
        var parsed = SkyGridModeParser.TryParse(value, out var mode);

        Assert.True(parsed);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void ParseOrDefault_Disables_Unknown_Grid_Values()
    {
        Assert.Equal(SkyGridMode.None, SkyGridModeParser.ParseOrDefault("unknown"));
    }

    [Theory]
    [InlineData(SkyGridMode.None, false, false)]
    [InlineData(SkyGridMode.Horizontal, true, false)]
    [InlineData(SkyGridMode.Equatorial, false, true)]
    [InlineData(SkyGridMode.Both, true, true)]
    public void Render_Policy_Selects_Requested_Grids(
        SkyGridMode mode,
        bool expectedHorizontal,
        bool expectedEquatorial)
    {
        Assert.Equal(expectedHorizontal, SkyGridModeParser.ShowsHorizontal(mode));
        Assert.Equal(expectedEquatorial, SkyGridModeParser.ShowsEquatorial(mode));
    }
}
