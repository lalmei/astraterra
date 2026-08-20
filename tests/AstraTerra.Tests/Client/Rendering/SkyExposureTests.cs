using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The two complaints this has to satisfy at once: a player under a tree keeps their sky, and a
/// player in a cave does not get one through the rock.
/// </summary>
public sealed class SkyExposureTests
{
    private const int OpenSkyLevel = 22;

    [Fact]
    public void Open_Sky_Overhead_Needs_No_Light_Check()
    {
        Assert.True(SkyExposure.CanSeeSky(rainMapHeight: 110, eyeHeight: 112, sunlightLevel: 0, OpenSkyLevel));
    }

    [Fact]
    public void A_Canopy_Or_Overhang_Still_Shows_The_Sky()
    {
        // Leaves and a porch roof block the rain map but barely dim the sky's own light.
        Assert.True(SkyExposure.CanSeeSky(rainMapHeight: 120, eyeHeight: 112, sunlightLevel: 20, OpenSkyLevel));
        Assert.True(SkyExposure.CanSeeSky(rainMapHeight: 120, eyeHeight: 112, sunlightLevel: OpenSkyLevel - SkyExposure.SunlightFalloffAllowance, OpenSkyLevel));
    }

    [Fact]
    public void A_Cave_Does_Not()
    {
        Assert.False(SkyExposure.CanSeeSky(rainMapHeight: 140, eyeHeight: 30, sunlightLevel: 0, OpenSkyLevel));
    }

    [Fact]
    public void A_Closed_Room_Does_Not()
    {
        // Deep enough inside that the doorway's light has fallen away.
        Assert.False(SkyExposure.CanSeeSky(rainMapHeight: 118, eyeHeight: 112, sunlightLevel: 9, OpenSkyLevel));
    }

    [Fact]
    public void The_Allowance_Is_Measured_Against_The_Worlds_Own_Light_Curve()
    {
        // A world configured with a shorter sunlight table still calls its own brightest level open
        // sky, rather than being judged against vanilla's 22.
        Assert.True(SkyExposure.CanSeeSky(rainMapHeight: 120, eyeHeight: 112, sunlightLevel: 8, maximumSunlightLevel: 15));
        Assert.False(SkyExposure.CanSeeSky(rainMapHeight: 120, eyeHeight: 112, sunlightLevel: 8, maximumSunlightLevel: 22));
    }
}
