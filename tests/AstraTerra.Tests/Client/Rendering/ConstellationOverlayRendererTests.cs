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

        var segments = ConstellationOverlayModel.BuildConstellationSegments(journal.Constellations, visibleStars);

        var segment = Assert.Single(segments);
        Assert.Equal(record.Id, segment.ConstellationId);
        Assert.Equal(10, segment.StartHip);
        Assert.Equal(20, segment.EndHip);
    }

    private static RenderedStar Star(int hip)
        => new(
            hip,
            VisualMagnitude: 1,
            AzimuthDeg: 0,
            AltitudeDeg: 45,
            Brightness: 1,
            ColorTemperatureK: 6500,
            IsGuideStar: true,
            DirectionX: 0,
            DirectionY: 1,
            DirectionZ: 0,
            Size: 1);
}
