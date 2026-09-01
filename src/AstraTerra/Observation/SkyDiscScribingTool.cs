using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>The simple edge a player must carry before putting a permanent mark on a sky disc.</summary>
public static class SkyDiscScribingTool
{
    public const string MissingMessage = "Keep a flint or knife in your hotbar to scratch the disc.";

    /// <summary>Whether any hotbar slot carries loose flint or an item classified as a knife.</summary>
    public static bool HasInHotbar(IPlayer? player)
    {
        var hotbar = player?.InventoryManager?.GetHotbarInventory();
        if (hotbar is null)
        {
            return false;
        }

        for (var index = 0; index < hotbar.Count; index++)
        {
            if (IsScribingTool(hotbar[index]?.Itemstack))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Recognizes vanilla loose flint and vanilla or modded knives.</summary>
    public static bool IsScribingTool(ItemStack? stack)
    {
        var collectible = stack?.Collectible;
        if (collectible?.Tool == EnumTool.Knife)
        {
            return true;
        }

        return string.Equals(collectible?.Code?.Path, "flint", StringComparison.OrdinalIgnoreCase);
    }
}
