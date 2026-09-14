using AstraTerra.Constellations;

namespace AstraTerra.Astronomy;

/// <summary>
/// Caches the drawn constellation half of the astrolabe target list.
/// </summary>
/// <remarks>
/// A journal is deserialized from the held book, so its object identity is not a useful revision
/// marker. The book identity catches a book swap, while the stored JSON catches an in-place write.
/// Planet observations and names remain outside this cache and are resolved live by the renderer on
/// every call.
/// </remarks>
public sealed class AstrolabeConstellationTargetCache
{
    private StarCatalog? catalog;
    private object? bookIdentity;
    private string? journalRevision;
    private IReadOnlyList<AstrolabeTarget>? targets;

    public IReadOnlyList<AstrolabeTarget> GetOrBuild(
        StarCatalog currentCatalog,
        object? currentBookIdentity,
        string? currentJournalRevision,
        Func<ConstellationJournal> readJournal)
    {
        ArgumentNullException.ThrowIfNull(currentCatalog);
        ArgumentNullException.ThrowIfNull(readJournal);

        if (targets is not null
            && ReferenceEquals(catalog, currentCatalog)
            && SameBook(bookIdentity, currentBookIdentity)
            && string.Equals(journalRevision, currentJournalRevision, StringComparison.Ordinal))
        {
            return targets;
        }

        targets = AstrolabeJournalTargets.Drawn(
            AstrolabeService.BuildTargets(readJournal(), currentCatalog));
        catalog = currentCatalog;
        bookIdentity = currentBookIdentity;
        journalRevision = currentJournalRevision;
        return targets;
    }

    private static bool SameBook(object? cached, object? current)
        => cached is string cachedId && current is string currentId
            ? string.Equals(cachedId, currentId, StringComparison.Ordinal)
            : ReferenceEquals(cached, current);
}
