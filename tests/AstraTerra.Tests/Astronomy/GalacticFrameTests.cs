using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// The band's whole position on the sky is this one rotation, so the landmarks it has to land on are
/// pinned here rather than judged by eye in game.
/// </summary>
public sealed class GalacticFrameTests
{
    [Fact]
    public void The_North_Galactic_Pole_Is_Where_The_Frame_Says_It_Is()
    {
        var pole = GalacticFrame.ToEquatorial(longitudeDeg: 0, latitudeDeg: 90);

        Assert.Equal(GalacticFrame.NorthPoleDeclinationDeg, pole.DeclinationDeg, precision: 5);
    }

    [Fact]
    public void The_Galactic_Centre_Lands_On_Sagittarius()
    {
        // Sagittarius A*, J2000: 17h 45m 40s, -29 deg 00'. The brightest part of the band has to sit
        // there, or the Milky Way rises in the wrong season and over the wrong horizon.
        var centre = GalacticFrame.ToEquatorial(longitudeDeg: 0, latitudeDeg: 0);

        Assert.Equal(266.405, centre.RightAscensionDeg, precision: 1);
        Assert.Equal(-28.936, centre.DeclinationDeg, precision: 1);
    }

    [Fact]
    public void The_Anticentre_Lands_Between_Auriga_And_Taurus()
    {
        // l = 180: 05h 46m, +28.9 deg — the faint side of the band, opposite the bulge.
        var anticentre = GalacticFrame.ToEquatorial(longitudeDeg: 180, latitudeDeg: 0);

        Assert.Equal(86.405, anticentre.RightAscensionDeg, precision: 1);
        Assert.Equal(28.936, anticentre.DeclinationDeg, precision: 1);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(45.0, 12.5)]
    [InlineData(210.0, -37.0)]
    [InlineData(359.0, 62.0)]
    public void The_Rotation_Undoes_Itself(double longitudeDeg, double latitudeDeg)
    {
        var equatorial = GalacticFrame.ToEquatorial(longitudeDeg, latitudeDeg);
        var galactic = GalacticFrame.FromEquatorial(equatorial.RightAscensionDeg, equatorial.DeclinationDeg);

        Assert.Equal(longitudeDeg, galactic.LongitudeDeg, precision: 6);
        Assert.Equal(latitudeDeg, galactic.LatitudeDeg, precision: 6);
    }
}
