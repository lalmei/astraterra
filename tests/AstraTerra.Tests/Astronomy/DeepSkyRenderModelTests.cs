using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class DeepSkyRenderModelTests
{
    [Fact]
    public void Project_Returns_Visible_Object_Above_Horizon()
    {
        var entry = new DeepSkyObjectEntry(
            "TEST",
            "Test Nebula",
            0,
            0,
            2.0,
            0.5,
            0.8f,
            0.9f,
            1.0f,
            "astraterra:environment/deep-sky/stellarium/test",
            ["astraterra:environment/deep-sky-cloud"]);

        var rendered = DeepSkyRenderModel.Project(entry, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.NotNull(rendered);
        Assert.Equal("TEST", rendered!.Id);
        Assert.InRange(rendered.Brightness, 0.49, 0.51);
        Assert.Equal(2.0, rendered.AngularSizeDeg);
        Assert.Equal(
            ["astraterra:environment/deep-sky/stellarium/test", "astraterra:environment/deep-sky-cloud"],
            rendered.TexturePaths);
    }

    [Fact]
    public void Project_Skips_Object_Below_Horizon()
    {
        var entry = new DeepSkyObjectEntry(
            "TEST",
            "Test Nebula",
            180,
            0,
            2.0,
            0.5,
            0.8f,
            0.9f,
            1.0f,
            "astraterra:environment/deep-sky/stellarium/test",
            ["astraterra:environment/deep-sky-cloud"]);

        var rendered = DeepSkyRenderModel.Project(entry, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.Null(rendered);
    }
}
