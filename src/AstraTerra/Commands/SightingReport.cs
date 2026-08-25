using AstraTerra.Observation;

namespace AstraTerra.Commands;

/// <summary>
/// The ledger as a listing, for <c>.stars sightings</c> and <c>.stars classify</c>.
/// </summary>
/// <remarks>
/// A book page is a fine record and a poor worktable: an observer deciding what they have been
/// looking at wants the sets side by side with their rates, and wants to point at one of them. Until
/// the chart lives on an instrument, this is where that work happens.
/// </remarks>
public static class SightingReport
{
    public static string Describe(ObservationLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (log.Observations.Count == 0)
        {
            return "No sightings written down yet. Sneak while sighting with the Sextant to write one down.";
        }

        var lines = new List<string> { $"Sightings written down: {log.Observations.Count}" };
        foreach (var record in log.Observations.OrderBy(record => record.Id))
        {
            var claim = log.FindClaimFor(record.Id);
            var conclusion = claim is null ? string.Empty : $"  [{claim.DisplayName}]";
            lines.Add($"#{record.Id} {record.Describe()}{conclusion}");
        }

        lines.Add(string.Empty);
        lines.Add("Comparisons:");
        foreach (var group in SightingComparison.Group(log.Observations))
        {
            lines.Add($"Set {group.Number}: {group.Describe()}");
        }

        lines.Add(string.Empty);
        lines.Add("Say what a set was with .stars classify <set> star|wanderer|comet [name]");

        return string.Join("\n", lines);
    }

    public static bool TryParseClass(string token, out SkyClass skyClass)
    {
        skyClass = token?.Trim().ToLowerInvariant() switch
        {
            "star" or "fixed" or "fixedstar" => SkyClass.FixedStar,
            "wanderer" or "planet" or "wandering" => SkyClass.Wanderer,
            "comet" => SkyClass.Comet,
            _ => SkyClass.Unclassified
        };

        return skyClass != SkyClass.Unclassified;
    }

    /// <summary>
    /// Turns what the observer typed into the entries they meant: a set number from the listing, or
    /// a single entry by its own number.
    /// </summary>
    public static IReadOnlyList<long>? ResolveEntries(ObservationLog log, string token, out string error)
    {
        ArgumentNullException.ThrowIfNull(log);

        var trimmed = token?.Trim() ?? string.Empty;
        if (trimmed.StartsWith('#'))
        {
            if (!long.TryParse(trimmed[1..], out var recordId)
                || log.Observations.All(record => record.Id != recordId))
            {
                error = $"No sighting {trimmed} in this book.";
                return null;
            }

            error = string.Empty;
            return [recordId];
        }

        if (!int.TryParse(trimmed, out var setNumber))
        {
            error = "Name a set from .stars sightings, or a single entry as #4.";
            return null;
        }

        var group = SightingComparison.Group(log.Observations)
            .FirstOrDefault(candidate => candidate.Number == setNumber);
        if (group is null)
        {
            error = $"No set {setNumber} in this book.";
            return null;
        }

        error = string.Empty;
        return group.Records.Select(record => record.Id).ToList();
    }
}
