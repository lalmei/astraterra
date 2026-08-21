using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Constellations;
using Vintagestory.API.MathTools;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class ConstellationOverlayRendererTests
{
    [Fact]
    public void BuildConstellationSegments_Returns_Only_Edges_With_Visible_Endpoints()
    {
        var journal = new ConstellationJournal();
        var record = journal.CreateFromEdges((10, 20), (20, 30));
        var visibleStars = new Dictionary<int, RenderedStar>
        {
            [10] = Star(10),
            [20] = Star(20)
        };

        var segments = ConstellationRenderModel.BuildConstellationSegments(journal.Constellations, visibleStars);

        var segment = Assert.Single(segments);
        Assert.Equal(record.Id, segment.ConstellationId);
        Assert.Equal(10, segment.StartHip);
        Assert.Equal(20, segment.EndHip);
    }

    [Fact]
    public void BuildSkyLines_Carries_Each_Edge_As_One_Tinted_Line()
    {
        var segment = new ConstellationSegment(
            ConstellationId: 7,
            StartHip: 10,
            EndHip: 20,
            Start: Star(10, directionX: 1, directionY: 0, directionZ: 0),
            End: Star(20, directionX: 0, directionY: 1, directionZ: 0));

        var line = Assert.Single(ConstellationRenderModel.BuildSkyLines([segment]));
        var tint = ConstellationRenderModel.GetConstellationTint(7);

        Assert.Equal(1.0, line.StartX, precision: 6);
        Assert.Equal(0.0, line.StartY, precision: 6);
        Assert.Equal(0.0, line.EndX, precision: 6);
        Assert.Equal(1.0, line.EndY, precision: 6);
        Assert.Equal(
            ColorUtil.ColorFromRgba(
                (int)Math.Round(tint.R * 255f),
                (int)Math.Round(tint.G * 255f),
                (int)Math.Round(tint.B * 255f),
                (int)Math.Round(tint.A * 255f)),
            line.Color);
    }

    /// <summary>An edge whose stars sit on top of each other has no direction to draw a line along.</summary>
    [Fact]
    public void BuildSkyLines_Drops_An_Edge_With_No_Length()
    {
        var segment = new ConstellationSegment(
            ConstellationId: 7,
            StartHip: 10,
            EndHip: 20,
            Start: Star(10, directionX: 1, directionY: 0, directionZ: 0),
            End: Star(20, directionX: 1, directionY: 0, directionZ: 0));

        Assert.Empty(ConstellationRenderModel.BuildSkyLines([segment]));
    }

    [Fact]
    public void GetConstellationTint_Assigns_Deterministic_Categorical_Palette()
    {
        var first = ConstellationRenderModel.GetConstellationTint(0);
        var second = ConstellationRenderModel.GetConstellationTint(1);
        var wrapped = ConstellationRenderModel.GetConstellationTint(12);
        var negativeWrapped = ConstellationRenderModel.GetConstellationTint(-12);

        Assert.NotEqual(first, second);
        Assert.Equal(first, wrapped);
        Assert.Equal(first, negativeWrapped);
        Assert.InRange(first.A, 0.30f, 0.40f);
    }

    private static RenderedStar Star(int hip, double directionX = 0, double directionY = 1, double directionZ = 0)
        => new(
            hip,
            new RenderedBody(
                Coordinates: new EquatorialCoordinates(0, 0),
                VisualMagnitude: 1,
                AzimuthDeg: 0,
                AltitudeDeg: 45,
                Brightness: 1,
                DirectionX: directionX,
                DirectionY: directionY,
                DirectionZ: directionZ,
                Size: 1),
            ColorTemperatureK: 6500,
            IsGuideStar: true);
}
