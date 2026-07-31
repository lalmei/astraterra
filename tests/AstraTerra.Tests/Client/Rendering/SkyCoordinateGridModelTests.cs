using AstraTerra.Client.Rendering;
using AstraTerra.Config;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class SkyCoordinateGridModelTests
{
    [Fact]
    public void None_Builds_No_Grid_Segments()
    {
        Assert.Empty(SkyCoordinateGridModel.Build(SkyGridMode.None, 45.0, 120.0));
    }

    [Fact]
    public void Horizontal_Builds_Only_Local_Horizon_Segments()
    {
        var segments = SkyCoordinateGridModel.Build(SkyGridMode.Horizontal, 45.0, 120.0);

        Assert.NotEmpty(segments);
        Assert.All(segments, segment =>
        {
            Assert.Equal(SkyCoordinateGridSystem.Horizontal, segment.System);
            Assert.True(segment.Start.Y >= -0.000001);
            Assert.True(segment.End.Y >= -0.000001);
            AssertUnitLength(segment.Start);
            AssertUnitLength(segment.End);
        });
    }

    [Fact]
    public void Equatorial_Grid_Is_Clipped_At_The_Local_Horizon()
    {
        var segments = SkyCoordinateGridModel.Build(SkyGridMode.Equatorial, 47.5, 213.0);

        Assert.NotEmpty(segments);
        Assert.All(segments, segment =>
        {
            Assert.Equal(SkyCoordinateGridSystem.Equatorial, segment.System);
            Assert.True(segment.Start.Y >= -0.000001);
            Assert.True(segment.End.Y >= -0.000001);
            AssertUnitLength(segment.Start);
            AssertUnitLength(segment.End);
        });
    }

    [Fact]
    public void Both_Combines_Horizontal_And_Equatorial_Segments()
    {
        var segments = SkyCoordinateGridModel.Build(SkyGridMode.Both, -33.0, 78.0);

        Assert.Contains(segments, segment => segment.System == SkyCoordinateGridSystem.Horizontal);
        Assert.Contains(segments, segment => segment.System == SkyCoordinateGridSystem.Equatorial);
        Assert.Contains(segments, segment => segment.IsReferenceLine);
    }

    [Fact]
    public void Equatorial_Grid_Rotates_With_Local_Sidereal_Angle()
    {
        var first = SkyCoordinateGridModel.Build(SkyGridMode.Equatorial, 35.0, 0.0);
        var second = SkyCoordinateGridModel.Build(SkyGridMode.Equatorial, 35.0, 45.0);

        Assert.NotEqual(first[0].Start, second[0].Start);
    }

    private static void AssertUnitLength(SkyGridDirection direction)
    {
        var length = Math.Sqrt(
            (direction.X * direction.X) +
            (direction.Y * direction.Y) +
            (direction.Z * direction.Z));
        Assert.InRange(length, 0.999999, 1.000001);
    }
}
