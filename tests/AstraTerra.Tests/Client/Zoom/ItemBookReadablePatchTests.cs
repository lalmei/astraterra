using AstraTerra.Client.Zoom.Patches;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Xunit;

namespace AstraTerra.Tests.Client.Zoom;

public sealed class ItemBookReadablePatchTests
{
    [Fact]
    public void Empty_Active_Slot_Is_Not_Readable()
    {
        var result = true;

        var runOriginal = ItemBookReadablePatch.Prefix(new DummySlot(), ref result);

        Assert.False(runOriginal);
        Assert.False(result);
    }

    [Fact]
    public void Populated_Slot_Is_Left_To_Vanilla()
    {
        var item = new Item
        {
            Attributes = JsonObject.FromJson("{\"readable\":true}")
        };
        var result = false;

        var runOriginal = ItemBookReadablePatch.Prefix(new DummySlot(new ItemStack(item)), ref result);

        Assert.True(runOriginal);
    }
}
