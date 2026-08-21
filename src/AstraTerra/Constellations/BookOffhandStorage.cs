using Vintagestory.API.Common;

namespace AstraTerra.Constellations;

/// <summary>
/// Lets a journal book be carried in the off-hand, which is where the mod reads it from.
/// </summary>
/// <remarks>
/// Vanilla books carry only <see cref="EnumItemStorageFlags.General"/>, so without this they cannot
/// enter the off-hand slot at all and every feature that reads a held journal goes quiet.
/// <para>
/// The mod also ships a JSON patch that sets the same flag. This code path exists because that patch
/// is silent when it does not take effect — the mod failing to load, or another mod rewriting
/// <c>itemtypes/lore/book</c> — and the symptom, "I cannot put my catalog in my off hand", looks
/// exactly like a bug in this mod. Setting the flag here as well means the only way to lose it is for
/// the mod not to run at all, which is a diagnosable state rather than a mystery.
/// </para>
/// </remarks>
public static class BookOffhandStorage
{
    public const string BookPathPrefix = "book-normal-";

    /// <summary>
    /// Whether an item is one of the vanilla writable books the journal is kept in.
    /// </summary>
    public static bool IsJournalBookCode(AssetLocation? code)
        => code is not null
           && string.Equals(code.Domain, "game", StringComparison.OrdinalIgnoreCase)
           && code.Path.StartsWith(BookPathPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Grants the off-hand flag to every writable book, and reports how many were changed.</summary>
    public static int Apply(ICoreAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var granted = 0;
        foreach (var item in api.World.Items)
        {
            if (!IsJournalBookCode(item?.Code) || (item!.StorageFlags & EnumItemStorageFlags.Offhand) != 0)
            {
                continue;
            }

            item.StorageFlags |= EnumItemStorageFlags.Offhand;
            granted++;
        }

        return granted;
    }
}
