using System.Text.Json;
using AstraTerra.Astronomy;
using AstraTerra.Observation;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstraTerra.Constellations;

public enum ConstellationJournalReadStatus
{
    Absent,
    Success,
    Unreadable
}

public sealed record ConstellationJournalReadResult(
    ConstellationJournalReadStatus Status,
    ConstellationJournal? Journal,
    int JsonLength,
    Exception? Exception);

public static class ConstellationBookService
{
    public const string BookTitle = "AstraTerra Constellation Journal";
    public const string StarCatalogTitle = "Star Catalog";
    public const string ZodiacTitle = "The Zodiac";
    public const string JournalJsonAttribute = "astraterraJournalJson";

    /// <summary>
    /// Planets live in their own attribute rather than inside the constellation journal, so a book
    /// written before planets existed still reads, and so writing one kind cannot disturb the other.
    /// </summary>
    public const string PlanetJournalJsonAttribute = "astraterraPlanetJson";

    /// <summary>
    /// Sightings live in a third attribute for the same reason planets live in a second one: a book
    /// written before the ledger existed still reads, and writing one half cannot disturb another.
    /// </summary>
    public const string ObservationLogJsonAttribute = "astraterraObservationJson";

    public const string PlanetCatalogTitle = "The Wanderers";
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

    /// <summary>
    /// Any book this mod has written a half of. Signing a book stamps it with vanilla text and a
    /// signature, which is exactly what <see cref="IsFreshWritableBookStack"/> rejects — so a book
    /// that holds only sightings or only wanderers has to be recognised by what we wrote into it,
    /// or the first thing written would be the last.
    /// </summary>
    public static bool IsAstraTerraBook(ItemStack? stack)
        => IsBookStack(stack)
           && (IsConstellationBook(stack) || IsPlanetBook(stack) || IsObservationBook(stack));

    public static bool IsValidJournalBook(ItemStack? stack)
        => IsAstraTerraBook(stack) || IsFreshWritableBookStack(stack);

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
        => ReadJournalResult(stack).Journal;

    public static ConstellationJournal? ReadJournal(ITreeAttribute attributes)
        => ReadJournalResult(attributes).Journal;

    public static ConstellationJournalReadResult ReadJournalResult(ItemStack? stack)
        => stack?.Attributes is null
            ? AbsentJournalResult()
            : ReadJournalResult(stack.Attributes);

    public static ConstellationJournalReadResult ReadJournalResult(ITreeAttribute attributes)
    {
        var json = attributes.GetString(JournalJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return AbsentJournalResult();
        }

        try
        {
            return new ConstellationJournalReadResult(
                ConstellationJournalReadStatus.Success,
                ConstellationPersistence.Deserialize(json),
                json.Length,
                null);
        }
        catch (Exception exception)
        {
            return new ConstellationJournalReadResult(
                ConstellationJournalReadStatus.Unreadable,
                null,
                json.Length,
                exception);
        }
    }

    public static ConstellationJournal ReadJournalOrEmpty(ItemStack? stack)
        => ReadJournal(stack) ?? new ConstellationJournal();

    public static PlanetJournal? ReadPlanetJournal(ItemStack? stack)
        => stack?.Attributes is null ? null : ReadPlanetJournal(stack.Attributes);

    public static PlanetJournal? ReadPlanetJournal(ITreeAttribute attributes)
    {
        var json = attributes.GetString(PlanetJournalJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return PlanetJournalPersistence.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }

    public static PlanetJournal ReadPlanetJournalOrEmpty(ItemStack? stack)
        => ReadPlanetJournal(stack) ?? new PlanetJournal();

    public static ObservationLog? ReadObservationLog(ItemStack? stack)
        => stack?.Attributes is null ? null : ReadObservationLog(stack.Attributes);

    public static ObservationLog? ReadObservationLog(ITreeAttribute attributes)
    {
        var json = attributes.GetString(ObservationLogJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return ObservationLogPersistence.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }

    public static ObservationLog ReadObservationLogOrEmpty(ItemStack? stack)
        => ReadObservationLog(stack) ?? new ObservationLog();

    public static bool IsObservationBook(ItemStack? stack)
        => stack?.Attributes?.HasAttribute(ObservationLogJsonAttribute) == true;

    /// <summary>
    /// Writes the sightings down, leaving the drawn figures and the wanderers alone.
    /// </summary>
    public static void WriteObservationLog(ItemStack stack, ObservationLog log, string? bookTitle = null)
        => WriteObservationLog(stack.Attributes, log, bookTitle);

    public static void WriteObservationLog(ITreeAttribute attributes, ObservationLog log, string? bookTitle = null)
    {
        ArgumentNullException.ThrowIfNull(log);

        var existingTitle = attributes.GetString(VanillaTitleAttribute, null);
        EnsureBookIdentity(
            attributes,
            !string.IsNullOrWhiteSpace(bookTitle) ? bookTitle.Trim() : ResolveBookTitle(existingTitle));
        attributes.SetString(ObservationLogJsonAttribute, ObservationLogPersistence.Serialize(log));
        attributes.SetString(VanillaTextAttribute, BuildReadableText(attributes));
    }

    /// <summary>
    /// Writes the planets down, leaving the constellation half of the book untouched. The readable
    /// page is rebuilt from both, because a reader sees one book.
    /// </summary>
    public static void WritePlanetJournal(ItemStack stack, PlanetJournal journal, string? bookTitle = null)
        => WritePlanetJournal(stack.Attributes, journal, bookTitle);

    public static void WritePlanetJournal(ITreeAttribute attributes, PlanetJournal journal, string? bookTitle = null)
    {
        ArgumentNullException.ThrowIfNull(journal);

        // Keeps whatever the book is already called: writing a planet into a Star Catalog must not
        // rename it to the default journal title.
        var existingTitle = attributes.GetString(VanillaTitleAttribute, null);
        EnsureBookIdentity(
            attributes,
            !string.IsNullOrWhiteSpace(bookTitle) ? bookTitle.Trim() : ResolveBookTitle(existingTitle));
        attributes.SetString(PlanetJournalJsonAttribute, PlanetJournalPersistence.Serialize(journal));
        attributes.SetString(VanillaTextAttribute, BuildReadableText(attributes));
    }

    public static bool IsPlanetBook(ItemStack? stack)
        => stack?.Attributes?.HasAttribute(PlanetJournalJsonAttribute) == true;

    public static string BuildReadableText(ConstellationJournal journal)
        => BuildReadableText(journal, new PlanetJournal());

    public static string BuildReadableText(ConstellationJournal journal, PlanetJournal planetJournal)
        => BuildReadableText(journal, planetJournal, new ObservationLog());

    /// <summary>Rebuilds the whole page from whichever halves the book already carries.</summary>
    private static string BuildReadableText(ITreeAttribute attributes)
        => BuildReadableText(
            ReadJournal(attributes) ?? new ConstellationJournal(),
            ReadPlanetJournal(attributes) ?? new PlanetJournal(),
            ReadObservationLog(attributes) ?? new ObservationLog());

    public static string BuildReadableText(
        ConstellationJournal journal,
        PlanetJournal planetJournal,
        ObservationLog observationLog)
    {
        ArgumentNullException.ThrowIfNull(planetJournal);
        ArgumentNullException.ThrowIfNull(observationLog);

        if (journal.Constellations.Count == 0
            && planetJournal.Planets.Count == 0
            && observationLog.Observations.Count == 0)
        {
            return "No constellations recorded.";
        }

        var lines = new List<string>();

        if (journal.Constellations.Count > 0)
        {
            lines.Add("Constellations");
            foreach (var record in journal.Constellations.OrderBy(record => record.Id))
            {
                lines.Add($"- {FormatDisplayName(record)}: stars={CountStars(record)}; segments={record.Edges.Count}");
            }
        }

        if (planetJournal.Planets.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Wandering stars");
            foreach (var record in planetJournal.Planets.OrderBy(record => record.RecordedTick))
            {
                lines.Add($"- {planetJournal.DisplayName(record.PlanetId)}");
            }
        }

        // Sightings read as a dated ledger and nothing else: a position, a moment and a brightness,
        // with no claim about what stood there. Comparing them is what turns them into a discovery.
        if (observationLog.Observations.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Sightings");
            foreach (var record in observationLog.Observations.OrderBy(record => record.Id))
            {
                lines.Add($"- #{record.Id} {record.Describe()}");
            }

            // The arithmetic, laid out but not concluded. The page says how far and how fast; what
            // that means is the observer's to decide, and their decision goes below it.
            var groups = SightingComparison.Group(observationLog.Observations);
            if (groups.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Comparisons");
                foreach (var group in groups)
                {
                    lines.Add($"- Set {group.Number}: {group.Describe()}");
                }
            }
        }

        if (observationLog.Claims.Count > 0)
        {
            if (lines.Count > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add("Findings");
            foreach (var claim in observationLog.Claims.OrderBy(claim => claim.Id))
            {
                var entries = string.Join(", ", claim.RecordIds.Select(id => $"#{id}"));
                lines.Add(
                    $"- {claim.DisplayName} — {SightingClaim.Describe(claim.Class).ToLowerInvariant()}, "
                    + $"{claim.Provenance} ({entries}), concluded day {claim.Day}");
            }
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
        var bookId = EnsureBookIdentity(attributes, resolvedBookTitle);

        attributes.SetString(JournalJsonAttribute, ConstellationPersistence.Serialize(journal));
        attributes.SetString(SkyCultureJsonAttribute, SerializeSkyCulture(journal, bookId, resolvedBookTitle));
        attributes.SetString(VanillaTextAttribute, BuildReadableText(attributes));
    }

    /// <summary>
    /// Stamps the identity and signature every AstraTerra book carries, whichever half is written.
    /// </summary>
    private static string EnsureBookIdentity(ITreeAttribute attributes, string resolvedBookTitle)
    {
        var bookId = attributes.GetString(BookIdAttribute, null);
        if (string.IsNullOrWhiteSpace(bookId))
        {
            bookId = Guid.NewGuid().ToString("N");
            attributes.SetString(BookIdAttribute, bookId);
        }

        attributes.SetInt(SchemaVersionAttribute, 1);
        attributes.SetString(LockedByAttribute, AstraTerraSignerUid);
        attributes.SetString(VanillaTitleAttribute, resolvedBookTitle);
        attributes.SetString(VanillaSignedByAttribute, AstraTerraSigner);
        attributes.SetString(VanillaSignedByUidAttribute, AstraTerraSignerUid);
        return bookId;
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

    private static ConstellationJournalReadResult AbsentJournalResult()
        => new(ConstellationJournalReadStatus.Absent, null, 0, null);
}
