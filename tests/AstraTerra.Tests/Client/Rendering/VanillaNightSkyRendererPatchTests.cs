using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class VanillaNightSkyRendererPatchTests
{
    [Fact]
    public void TargetMethod_Resolves_Vanilla_Night_Sky_Render_Pass()
    {
        var method = VanillaNightSkyRendererPatch.TargetMethod();

        Assert.Equal("OnRenderFrame3D", method.Name);
        Assert.Equal("SystemRenderNightSky", method.DeclaringType?.Name);
        Assert.Equal([typeof(float)], method.GetParameters().Select(parameter => parameter.ParameterType));
    }
}
