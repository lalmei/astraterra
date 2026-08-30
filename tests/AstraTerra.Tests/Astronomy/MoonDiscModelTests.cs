using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class MoonDiscModelTests
{
    [Fact]
    public void The_Eight_Faces_Run_New_To_Full_And_Back()
    {
        Assert.Equal(
            ["new", "waxing-crescent", "first-quarter", "waxing-gibbous", "full", "waning-gibbous", "last-quarter", "waning-crescent"],
            MoonDiscModel.Faces.Select(face => face.Id));

        // Waxing faces are lit on the right, waning ones on the left, which is what turns each
        // picture the right way round against the sun.
        Assert.All(MoonDiscModel.Faces.Take(5), face => Assert.True(face.LitOnRight));
        Assert.All(MoonDiscModel.Faces.Skip(5), face => Assert.False(face.LitOnRight));
    }

    /// <summary>
    /// The calendar's phase runs 0 to 8 and then begins again, so a phase just short of eight is a new
    /// moon rather than a waning crescent: the month has to close.
    /// </summary>
    [Fact]
    public void The_Face_Follows_The_Calendars_Phase_Around_The_Month()
    {
        Assert.Equal("new", MoonDiscModel.SelectFace(0.0).Id);
        Assert.Equal("waxing-crescent", MoonDiscModel.SelectFace(1.2).Id);
        Assert.Equal("full", MoonDiscModel.SelectFace(4.0).Id);
        Assert.Equal("waning-crescent", MoonDiscModel.SelectFace(7.4).Id);
        Assert.Equal("new", MoonDiscModel.SelectFace(7.9).Id);
        Assert.Equal("new", MoonDiscModel.SelectFace(8.0).Id);
    }

    /// <summary>
    /// The thing everybody notices when it is wrong: the bright limb points at the sun, wherever the
    /// sun is and whichever edge of the picture that limb was drawn on.
    /// </summary>
    [Fact]
    public void The_Bright_Limb_Points_At_The_Sun()
    {
        var moon = new SkyDirection(0.0, 0.6, -0.8);            // up in the north
        var sun = new SkyDirection(0.8, -0.6, 0.0);             // down in the east, as at moonrise

        var waxing = MoonDiscModel.RightAxis(moon, sun, litOnRight: true);
        var waning = MoonDiscModel.RightAxis(moon, sun, litOnRight: false);

        // The picture's lit edge and the sun are on the same side of the moon in both cases: the
        // right edge for a waxing face, the left for a waning one.
        Assert.True(Dot(waxing, sun) > 0.5);
        Assert.True(Dot(waning, sun) < -0.5);

        // And both are tangents: an axis with any lean towards or away from the observer would skew
        // the picture rather than turn it.
        Assert.Equal(0.0, Dot(waxing, moon), 9);
        Assert.Equal(0.0, Dot(waning, moon), 9);
    }

    [Fact]
    public void A_Full_Moon_Hangs_Level_Rather_Than_Pointing_Nowhere()
    {
        var moon = new SkyDirection(0.0, 0.6, -0.8);

        // The sun directly behind the observer has no direction on the sky to turn towards, and a
        // full face has no limb that needs one.
        var axis = MoonDiscModel.RightAxis(moon, new SkyDirection(0.0, -0.6, 0.8), litOnRight: true);

        Assert.Equal(1.0, Math.Sqrt(Dot(axis, axis)), 9);
        Assert.Equal(0.0, axis.Y, 9);
        Assert.Equal(0.0, Dot(axis, moon), 9);
    }

    /// <summary>
    /// The replacement occupies the same roughly seven-degree width as Vintage Story's own moon.
    /// </summary>
    [Fact]
    public void The_Moon_Matches_The_Vanilla_Apparent_Size()
    {
        var moon = new SkyDirection(0.0, 1.0, 0.0);

        var corners = MoonDiscModel.BuildQuad(moon, new SkyDirection(1.0, 0.0, 0.0), MoonDiscModel.Faces[4]);

        Assert.Equal(4, corners.Count);
        Assert.Equal(7.0, MoonDiscModel.AngularDiameterDeg);
        // Half a diagonal from the centre, measured on the sphere: the corners are laid out on the
        // tangent plane and then dropped onto it, which pulls them in by a fraction of an arcminute.
        var halfWidth = MoonDiscModel.AngularDiameterDeg * Math.PI / 360.0;
        var halfDiagonalDeg = Math.Atan(Math.Sqrt(2.0) * halfWidth) * 180.0 / Math.PI;
        Assert.All(corners, corner => Assert.Equal(halfDiagonalDeg, AngleDeg(corner, moon), 6));
    }

    private static double Dot(SkyDirection left, SkyDirection right)
        => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static double AngleDeg(DeepSkyDirection corner, SkyDirection direction)
    {
        var dot = (corner.X * direction.X) + (corner.Y * direction.Y) + (corner.Z * direction.Z);
        return Math.Acos(Math.Clamp(dot, -1.0, 1.0)) * 180.0 / Math.PI;
    }
}
