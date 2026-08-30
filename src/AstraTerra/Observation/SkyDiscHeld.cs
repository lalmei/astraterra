using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>
/// Finding the disc a player is holding, in either hand.
/// </summary>
/// <remarks>
/// A disc is read from the hand it is in. The right hand is the one that raises it and the one that
/// gets engraved, and the off hand is enough to keep the figure on it showing in the sky — the same
/// bargain the journal book makes, and for the same reason: an instrument you have put away is not
/// an instrument you are using.
/// <para>
/// Matched by code rather than by item class so that the code that reads a held disc does not have
/// to know about the item that implements one.
/// </para>
/// </remarks>
public static class SkyDiscHeld
{
    public const string DiscPathPrefix = "sky-disc";

    public static bool IsDisc(ItemStack? stack) => IsDisc(stack?.Collectible?.Code);

    public static bool IsDisc(AssetLocation? code)
        => code is not null
           && string.Equals(code.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
           && code.Path.StartsWith(DiscPathPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The disc being held, right hand before left, or nothing.</summary>
    public static ItemSlot? FindSlot(IPlayer? player)
    {
        var entity = player?.Entity;
        if (entity is null)
        {
            return null;
        }

        if (IsDisc(entity.RightHandItemSlot?.Itemstack))
        {
            return entity.RightHandItemSlot;
        }

        return IsDisc(entity.LeftHandItemSlot?.Itemstack) ? entity.LeftHandItemSlot : null;
    }

    public static ItemStack? Find(IPlayer? player) => FindSlot(player)?.Itemstack;

    public static bool IsHolding(IPlayer? player) => FindSlot(player) is not null;
}
