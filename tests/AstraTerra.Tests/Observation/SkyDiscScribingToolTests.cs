using AstraTerra.Observation;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SkyDiscScribingToolTests
{
    [Fact]
    public void Loose_Flint_Can_Scratch_A_Disc()
    {
        var flint = new Item { Code = new AssetLocation("game", "flint") };

        Assert.True(SkyDiscScribingTool.IsScribingTool(new ItemStack(flint)));
    }

    [Fact]
    public void Any_Item_Classified_As_A_Knife_Can_Scratch_A_Disc()
    {
        var knife = new Item
        {
            Code = new AssetLocation("anothermod", "carvingblade"),
            Tool = EnumTool.Knife
        };

        Assert.True(SkyDiscScribingTool.IsScribingTool(new ItemStack(knife)));
    }

    [Fact]
    public void An_Unrelated_Item_Cannot_Scratch_A_Disc()
    {
        var stick = new Item { Code = new AssetLocation("game", "stick") };

        Assert.False(SkyDiscScribingTool.IsScribingTool(new ItemStack(stick)));
        Assert.False(SkyDiscScribingTool.IsScribingTool(null));
    }
}
