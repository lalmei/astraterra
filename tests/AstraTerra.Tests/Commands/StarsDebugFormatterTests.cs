using AstraTerra.Commands;
using Xunit;

namespace AstraTerra.Tests.Commands;

public sealed class StarsDebugFormatterTests
{
    [Fact]
    public void Unnamed_Constellation_Label_Includes_Id()
    {
        var label = StarsDebugFormatter.FormatDisplayName(name: null, id: 12);
        Assert.Equal("Unnamed Constellation (#12)", label);
    }

    [Fact]
    public void Debug_Output_Includes_Hovered_And_Selected_When_Available()
    {
        var output = StarsDebugFormatter.FormatDebug(
            latitudeDeg: 45,
            latitudeWrapCycle: 1,
            siderealAngleDeg: 120,
            gatingReason: "ok",
            hoveredGuideHipId: 100,
            selectedConstellationId: 5,
            guideStarEmphasis: true);

        Assert.Contains("hovered=100", output);
        Assert.Contains("selected=5", output);
        Assert.Contains("guideEmphasis=true", output);
    }
}
