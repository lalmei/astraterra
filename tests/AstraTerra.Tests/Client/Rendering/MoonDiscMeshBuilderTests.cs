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

    [Fact]
    public void A_Moon_Over_The_Sun_Blocks_It()
    {
        var occluder = MoonDiscMeshBuilder.BuildSunOccluder(Moon, Moon, radius: 40f);

        // The centre vertex of the grid sits on the sun: black, and opaque.
        var centre = ((MoonDiscMeshBuilder.Subdivisions / 2 * (MoonDiscMeshBuilder.Subdivisions + 1))
            + (MoonDiscMeshBuilder.Subdivisions / 2)) * 4;
        Assert.Equal(0, occluder.Rgba[centre]);
        Assert.Equal(255, occluder.Rgba[centre + 3]);

        // A corner is well off the sun's disc, and blocks nothing there.
        Assert.Equal(0, occluder.Rgba[3]);
    }

    [Fact]
    public void Only_The_Sun_Is_Blocked_Not_The_Sky_Around_It()
    {
        Assert.Equal(1f, MoonDiscMeshBuilder.SunCoverage(Moon, Moon), precision: 5);

        var justOffTheDisc = Tilt(Moon, MoonDiscMeshBuilder.SunCoreRadiusDeg + MoonDiscMeshBuilder.SunEdgeFadeDeg + 0.1);
        Assert.Equal(0f, MoonDiscMeshBuilder.SunCoverage(justOffTheDisc, Moon), precision: 5);
    }

    [Fact]
    public void The_Sun_Pass_Runs_Only_Near_Conjunction()
    {
        Assert.True(MoonDiscMeshBuilder.CanCoverSun(Moon, Tilt(Moon, 5.0)));
        Assert.False(MoonDiscMeshBuilder.CanCoverSun(Moon, Tilt(Moon, 10.0)));
    }

    /// <summary>A direction the given number of degrees from <paramref name="from"/>, towards +X.</summary>
    private static SkyDirection Tilt(SkyDirection from, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new SkyDirection(Math.Sin(radians), from.Y, from.Z * Math.Cos(radians));
    }
}
