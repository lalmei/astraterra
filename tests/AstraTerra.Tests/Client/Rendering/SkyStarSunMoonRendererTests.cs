using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyStarSunMoonRendererTests
{
    [Theory]
    [InlineData(0.00, false, false)]
    [InlineData(0.04, false, false)]
    [InlineData(0.10, false, false)]
    [InlineData(0.11, false, true)]
    [InlineData(0.00, true, true)]
    public void ShouldRenderForDarkness_Skips_Near_Daylight_Unless_Forced(double naturalDarkness, bool forceDaylight, bool expected)
    {
        var shouldRender = SkyStarSunMoonRenderer.ShouldRenderForDarkness(naturalDarkness, forceDaylight);

        Assert.Equal(expected, shouldRender);
    }
}
