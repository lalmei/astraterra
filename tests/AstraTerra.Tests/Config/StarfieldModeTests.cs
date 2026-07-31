using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Config;

public sealed class StarfieldModeTests
{
    [Theory]
    [InlineData("astraterra", StarfieldMode.AstraTerra)]
    [InlineData("astra", StarfieldMode.AstraTerra)]
    [InlineData("BOTH", StarfieldMode.Both)]
    [InlineData(" vanilla ", StarfieldMode.Vanilla)]
    public void TryParse_Accepts_Supported_Config_Values(string value, StarfieldMode expected)
    {
        var parsed = StarfieldModeParser.TryParse(value, out var mode);

        Assert.True(parsed);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void ParseOrDefault_Uses_AstraTerra_For_Unknown_Values()
    {
        Assert.Equal(StarfieldMode.AstraTerra, StarfieldModeParser.ParseOrDefault("unknown"));
    }

    [Theory]
    [InlineData(StarfieldMode.AstraTerra, true, false)]
    [InlineData(StarfieldMode.Both, true, true)]
    [InlineData(StarfieldMode.Vanilla, false, true)]
    public void Render_Policy_Selects_Requested_Starfields(
        StarfieldMode mode,
        bool expectedAstraTerra,
        bool expectedVanilla)
    {
        Assert.Equal(expectedAstraTerra, StarfieldModeParser.ShowsAstraTerra(mode));
        Assert.Equal(expectedVanilla, StarfieldModeParser.ShowsVanilla(mode));
    }
}
