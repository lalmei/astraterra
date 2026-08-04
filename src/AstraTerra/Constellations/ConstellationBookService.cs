using System.Text.Json;
using AstraTerra.Astronomy;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstraTerra.Constellations;

public static class ConstellationBookService
{
    public const string BookTitle = "AstraTerra Constellation Journal";
    public const string StarCatalogTitle = "Star Catalog";
    public const string ZodiacTitle = "The Zodiac";
    public const string JournalJsonAttribute = "astraterraJournalJson";
    public const string SkyCultureJsonAttribute = "astraterraSkyCultureJson";
    public const string BookIdAttribute = "astraterraBookId";
    public const string SchemaVersionAttribute = "astraterraBookSchemaVersion";
    public const string LockedByAttribute = "astraterraLockedBy";
    public const string VanillaTextAttribute = "text";
    public const string VanillaTitleAttribute = "title";
    public const string VanillaSignedByAttribute = "signedby";
    public const string VanillaSignedByUidAttribute = "signedbyuid";
    public const string AstraTerraSigner = "AstraTerra";
    public const string AstraTerraSignerUid = "astraterra";

    private static readonly JsonSerializerOptions SkyCultureOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static bool IsBookStack(ItemStack? stack)
    {
        var path = stack?.Collectible?.Code?.Path;
        return path is not null && path.StartsWith("book-", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFreshWritableBookStack(ItemStack? stack)
    {
        var path = stack?.Collectible?.Code?.Path;
        return path is not null &&
               path.StartsWith("book-normal-", StringComparison.OrdinalIgnoreCase) &&
               !IsConstellationBook(stack) &&
               !HasVanillaSignature(stack) &&
               !HasVanillaText(stack);
    }

    public static bool IsConstellationBook(ItemStack? stack)
        => stack?.Attributes?.HasAttribute(JournalJsonAttribute) == true;

    public static bool IsReadableConstellationBook(ItemStack? stack)
        => IsBookStack(stack) && IsConstellationBook(stack);

    public static bool IsValidJournalBook(ItemStack? stack)
        => IsReadableConstellationBook(stack) || IsFreshWritableBookStack(stack);

    public static bool IsInkAndQuill(ItemStack? stack)
    {
        var code = stack?.Collectible?.Code;
        return string.Equals(code?.Path, "inkandquill", StringComparison.OrdinalIgnoreCase);
    }

    public static bool PlayerHasInkAndQuill(IPlayer player)
        => player.InventoryManager.Find(slot => IsInkAndQuill(slot.Itemstack));

    public static ItemSlot? GetLeftHandBookSlot(IPlayer player)
    {
        var slot = player.Entity.LeftHandItemSlot;
        return IsValidJournalBook(slot?.Itemstack) ? slot : null;
    }

    public static ConstellationJournal? ReadJournal(ItemStack? stack)
        => stack?.Attributes is null ? null : ReadJournal(stack.Attributes);

    public static ConstellationJournal? ReadJournal(ITreeAttribute attributes)
    {
        var json = attributes.GetString(JournalJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return ConstellationPersistence.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }

    public static ConstellationJournal ReadJournalOrEmpty(ItemStack? stack)
        => ReadJournal(stack) ?? new ConstellationJournal();

    public static string BuildReadableText(ConstellationJournal journal)
    {
        if (journal.Constellations.Count == 0)
        {
            return "No constellations recorded.";
        }

        var lines = new List<string>
        {
            "Constellations"
        };

        foreach (var record in journal.Constellations.OrderBy(record => record.Id))
        {
            lines.Add($"- {FormatDisplayName(record)}: stars={CountStars(record)}; segments={record.Edges.Count}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static SkyCultureConstellationSet ToSkyCulture(ConstellationJournal journal, string bookId, string? bookTitle = null)
        => new(
            1,
            $"astraterra-book-{bookId}",
            ResolveBookTitle(bookTitle),
            ["book", "personal"],
            new SkyCultureSource(
                "AstraTerra constellation book",
                "https://mods.vintagestory.at/",
                "Player-authored",
                "Generated from a written AstraTerra constellation journal book."),
            journal.Constellations
                .OrderBy(record => record.Id)
                .Select(record => new SkyCultureConstellation(
                    $"AT-{record.Id}",
                    FormatDisplayName(record),
                    record.Edges
                        .OrderBy(edge => edge.EdgeOrder)
                        .Select(edge => (IReadOnlyList<int>)[edge.A, edge.B])
                        .ToList()))
                .ToList());

    public static string SerializeSkyCulture(ConstellationJournal journal, string bookId, string? bookTitle = null)
        => JsonSerializer.Serialize(ToSkyCulture(journal, bookId, bookTitle), SkyCultureOptions);

    public static void WriteJournal(ItemStack stack, ConstellationJournal journal, string? bookTitle = null)
        => WriteJournal(stack.Attributes, journal, bookTitle);

    public static void WriteJournal(ITreeAttribute attributes, ConstellationJournal journal, string? bookTitle = null)
    {
        var resolvedBookTitle = ResolveBookTitle(bookTitle);
        var bookId = attributes.GetString(BookIdAttribute, null);
        if (string.IsNullOrWhiteSpace(bookId))
        {
            bookId = Guid.NewGuid().ToString("N");
            attributes.SetString(BookIdAttribute, bookId);
        }

        attributes.SetInt(SchemaVersionAttribute, 1);
        attributes.SetString(LockedByAttribute, AstraTerraSignerUid);
        attributes.SetString(JournalJsonAttribute, ConstellationPersistence.Serialize(journal));
        attributes.SetString(SkyCultureJsonAttribute, SerializeSkyCulture(journal, bookId, resolvedBookTitle));
        attributes.SetString(VanillaTitleAttribute, resolvedBookTitle);
        attributes.SetString(VanillaTextAttribute, BuildReadableText(journal));
        attributes.SetString(VanillaSignedByAttribute, AstraTerraSigner);
        attributes.SetString(VanillaSignedByUidAttribute, AstraTerraSignerUid);
    }

    public static string FormatDisplayName(ConstellationRecord record)
        => string.IsNullOrWhiteSpace(record.Name) ? $"Unnamed Constellation (#{record.Id})" : record.Name.Trim();

    public static int CountStars(ConstellationRecord record)
        => record.Edges.SelectMany(edge => new[] { edge.A, edge.B }).Distinct().Count();

    private static bool HasVanillaSignature(ItemStack? stack)
        => stack?.Attributes?.HasAttribute(VanillaSignedByAttribute) == true;

    private static bool HasVanillaText(ItemStack? stack)
        => stack?.Attributes?.HasAttribute(VanillaTextAttribute) == true ||
           stack?.Attributes?.HasAttribute("textCodes") == true;

    private static string ResolveBookTitle(string? bookTitle)
        => string.IsNullOrWhiteSpace(bookTitle) ? BookTitle : bookTitle.Trim();
}
