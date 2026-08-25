namespace AstraTerra.Observation;

/// <summary>
/// What a set of sightings turned out to be, as far as the observer is concerned.
/// </summary>
/// <remarks>
/// These are the classes that can be told apart by the shape of a record set alone — something that
/// keeps its place, something that does not, and something that does not and then stops being there.
/// Deep sky objects and meteor showers are genuinely different discoveries, made from record sets of
/// a different shape, and they are missing here on purpose rather than by oversight.
/// </remarks>
public enum SkyClass
{
    /// <summary>Sighted, and nothing said about it yet.</summary>
    Unclassified = 0,

    /// <summary>It held its place against its neighbours, night after night.</summary>
    FixedStar = 1,

    /// <summary>It moved against them.</summary>
    Wanderer = 2,

    /// <summary>It moved fast, and then there was nothing left to sight.</summary>
    Comet = 3
}

/// <summary>
/// One conclusion the observer drew, and the entries they drew it from.
/// </summary>
/// <remarks>
/// Kept apart from the entries themselves so that a wrong conclusion costs nothing but the
/// conclusion. The evidence is what was measured; this is only a reading of it, and it carries the
/// entries it rests on so the book can always say how much was behind it.
/// </remarks>
/// <param name="Day">The world day the observer made the call, not the day of any sighting.</param>
public sealed record SightingClaim(
    long Id,
    SkyClass Class,
    string? Name,
    IReadOnlyList<long> RecordIds,
    int Day
)
{
    public string DisplayName
        => string.IsNullOrWhiteSpace(Name) ? $"Unnamed {Describe(Class).ToLowerInvariant()}" : Name.Trim();

    public static string Describe(SkyClass skyClass)
        => skyClass switch
        {
            SkyClass.FixedStar => "Fixed star",
            SkyClass.Wanderer => "Wandering star",
            SkyClass.Comet => "Comet",
            _ => "Unclassified"
        };

    /// <summary>How much is behind the conclusion, so no answer derived from it can hide its thinness.</summary>
    public string Provenance
        => RecordIds.Count == 1
            ? "from 1 sighting"
            : $"from {RecordIds.Count} sightings";
}
