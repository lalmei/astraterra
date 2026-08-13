using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using AstraTerra.Constellations;
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
    public void BuildSkyDots_Returns_Normalized_Interior_Dots()
    {
        var segment = new ConstellationSegment(
            ConstellationId: 7,
            StartHip: 10,
            EndHip: 20,
            Start: Star(10, directionX: 1, directionY: 0, directionZ: 0),
            End: Star(20, directionX: 0, directionY: 1, directionZ: 0));

        var dot = Assert.Single(ConstellationRenderModel.BuildSkyDots([segment], angularSpacingDeg: 45));
        var length = Math.Sqrt((dot.DirectionX * dot.DirectionX) + (dot.DirectionY * dot.DirectionY) + (dot.DirectionZ * dot.DirectionZ));

        Assert.Equal(7, dot.ConstellationId);
        Assert.Equal(10, dot.StartHip);
        Assert.Equal(20, dot.EndHip);
        Assert.Equal(ConstellationRenderModel.GetConstellationTint(7), dot.Tint);
        Assert.Equal(1.0, length, precision: 6);
        Assert.Equal(Math.Sqrt(0.5), dot.DirectionX, precision: 6);
        Assert.Equal(Math.Sqrt(0.5), dot.DirectionY, precision: 6);
        Assert.Equal(0, dot.DirectionZ, precision: 6);
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
