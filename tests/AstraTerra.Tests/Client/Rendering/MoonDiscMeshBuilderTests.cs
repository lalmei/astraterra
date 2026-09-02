using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class MoonDiscMeshBuilderTests
{
    private static readonly SkyDirection Moon = new(0.0, 0.0, -1.0);

    [Fact]
    public void Turning_The_Terminator_Does_Not_Turn_The_Surface()
    {
        var lightFromRight = MoonDiscMeshBuilder.Build(
            Moon,
            new SkyDirection(1.0, 0.0, 0.0),
            moonPhaseExact: 2.0,
            radius: 40f);
        var lightFromAbove = MoonDiscMeshBuilder.Build(
            Moon,
            new SkyDirection(0.0, 1.0, 0.0),
            moonPhaseExact: 2.0,
            radius: 40f);

        Assert.Equal(lightFromRight.xyz, lightFromAbove.xyz);
        Assert.Equal(lightFromRight.Uv, lightFromAbove.Uv);
        Assert.NotEqual(lightFromRight.Rgba, lightFromAbove.Rgba);
    }

    [Fact]
    public void The_Bright_Half_Points_At_The_Sun()
    {
        var sun = new SkyDirection(1.0, 0.0, 0.0);

        var left = MoonDiscMeshBuilder.LightAt(-0.5, 0.0, Moon, sun, moonPhaseExact: 2.0);
        var right = MoonDiscMeshBuilder.LightAt(0.5, 0.0, Moon, sun, moonPhaseExact: 2.0);

        Assert.Equal(MoonDiscMeshBuilder.Earthshine, left, precision: 5);
        Assert.Equal(1f, right, precision: 5);
    }

    [Fact]
    public void The_Phase_Changes_Continuously_Between_Authored_Eighths()
    {
        var sun = new SkyDirection(1.0, 0.0, 0.0);

        var crescent = MoonDiscMeshBuilder.LightAt(0.4, 0.0, Moon, sun, moonPhaseExact: 1.0);
        var between = MoonDiscMeshBuilder.LightAt(0.4, 0.0, Moon, sun, moonPhaseExact: 1.5);
        var quarter = MoonDiscMeshBuilder.LightAt(0.4, 0.0, Moon, sun, moonPhaseExact: 2.0);

        Assert.True(crescent < between);
        Assert.True(between < quarter);
    }
}
