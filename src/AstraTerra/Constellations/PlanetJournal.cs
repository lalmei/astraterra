using System.Text.Json;

namespace AstraTerra.Constellations;

/// <summary>
/// One wanderer an observer has identified and written down.
/// </summary>
/// <param name="PlanetId">The catalog id the sky model knows it by, which the book never shows.</param>
/// <param name="Name">
/// What the observer decided to call it. Null until they name it, which is the same state a drawn
/// constellation starts in.
/// </param>
public sealed record PlanetRecord(
    string PlanetId,
    string? Name,
    long RecordedTick
);

/// <summary>
/// The planets an observer has picked out of the sky, and what they chose to call them.
/// </summary>
/// <remarks>
/// A planet is not discovered by resolving detail in it — through any telescope it stays a point —
/// but by noticing that it moves against the stars while everything around it does not. So the
/// journal records the act of identifying one, and the name is the observer's own. Nothing in the sky
/// model hands them "Mars"; that name arrives only in a prepared book.
/// </remarks>
public sealed class PlanetJournal
{
    private readonly List<PlanetRecord> planets;
    private long nextTick;

    public PlanetJournal()
        : this(1, [])
    {
    }

    internal PlanetJournal(long nextTick, IEnumerable<PlanetRecord> planets)
    {
        this.nextTick = nextTick;
        this.planets = planets.ToList();
    }

    public IReadOnlyList<PlanetRecord> Planets => planets;

    /// <summary>What an observer with this book in hand calls a planet, and what it reads as if not.</summary>
    public const string UnidentifiedDisplayName = "Wandering star";

    public PlanetRecord? Find(string planetId)
        => planets.FirstOrDefault(record => string.Equals(record.PlanetId, planetId, StringComparison.Ordinal));

    public bool IsIdentified(string planetId) => Find(planetId) is not null;

    /// <summary>
    /// Writes a planet down, or returns the existing entry when it is already known. Identifying is
    /// separate from naming so an observer can record a sighting now and decide what to call it later.
    /// </summary>
    public PlanetRecord Identify(string planetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planetId);

        var existing = Find(planetId);
        if (existing is not null)
        {
            return existing;
        }

        var record = new PlanetRecord(planetId, null, nextTick++);
        planets.Add(record);
        return record;
    }

    /// <summary>Names a planet, identifying it first if this is the observer's first sight of it.</summary>
    public PlanetRecord Rename(string planetId, string? name)
    {
        var record = Identify(planetId);
        var trimmed = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var renamed = record with { Name = trimmed };
        planets[planets.IndexOf(record)] = renamed;
        return renamed;
    }

    public bool Remove(string planetId)
    {
        var record = Find(planetId);
        return record is not null && planets.Remove(record);
    }

    /// <summary>
    /// What to call a planet: the observer's name, then a placeholder once identified but unnamed,
    /// and the same "wandering star" it looked like before that.
    /// </summary>
    public string DisplayName(string planetId)
    {
        var record = Find(planetId);
        if (record is null)
        {
            return UnidentifiedDisplayName;
        }

        return string.IsNullOrWhiteSpace(record.Name)
            ? $"{UnidentifiedDisplayName} (unnamed)"
            : record.Name.Trim();
    }

    internal PlanetJournalSnapshot ToSnapshot() => new(1, nextTick, planets.ToList());

    internal static PlanetJournal FromSnapshot(PlanetJournalSnapshot snapshot)
        => new(
            Math.Max(1, snapshot.NextTick),
            (snapshot.Planets ?? [])
                .Where(record => !string.IsNullOrWhiteSpace(record.PlanetId))
                .ToList());
}

internal sealed record PlanetJournalSnapshot(
    int SchemaVersion,
    long NextTick,
    IReadOnlyList<PlanetRecord> Planets
);

public static class PlanetJournalPersistence
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(PlanetJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);

        return JsonSerializer.Serialize(journal.ToSnapshot(), SerializerOptions);
    }

    public static PlanetJournal Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<PlanetJournalSnapshot>(json, SerializerOptions)
                       ?? throw new InvalidOperationException("Planet journal JSON was empty.");
        return PlanetJournal.FromSnapshot(snapshot);
    }
}
