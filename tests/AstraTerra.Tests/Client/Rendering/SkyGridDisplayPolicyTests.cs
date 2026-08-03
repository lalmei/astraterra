using AstraTerra.Client.Rendering;
using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyGridDisplayPolicyTests
{
    [Theory]
    [InlineData(SkyGridMode.None)]
    [InlineData(SkyGridMode.Horizontal)]
    [InlineData(SkyGridMode.Equatorial)]
    [InlineData(SkyGridMode.Both)]
    public void Resolve_Uses_Sextant_Mode_While_Sextant_Is_In_Use(SkyGridMode sextantMode)
    {
        Assert.Equal(
            sextantMode,
            SkyGridDisplayPolicy.Resolve(SkyGridMode.Both, sextantInUse: true, sextantMode));
    }

    [Theory]
    [InlineData(SkyGridMode.None)]
    [InlineData(SkyGridMode.Horizontal)]
    [InlineData(SkyGridMode.Equatorial)]
    [InlineData(SkyGridMode.Both)]
    public void Resolve_Restores_Configured_Mode_When_Sextant_Is_Released(SkyGridMode configuredMode)
    {
        Assert.Equal(
            configuredMode,
            SkyGridDisplayPolicy.Resolve(configuredMode, sextantInUse: false, SkyGridMode.None));
    }
}
