using AstraTerra.Constellations;
using AstraTerra.Observation;

namespace AstraTerra.Astronomy;

/// <summary>
/// Narrows the astrolabe's targets to what the observer's own book knows, and says what each answer
/// rests on.
/// </summary>
/// <remarks>
/// The instrument computes from the observer's record, not from the game's catalog. A wandering body
/// nobody has picked out of the sky is not a target that reads "unknown" — it is not a target at all,
/// because there is nothing in the observer's model for the instrument to aim with. What they have
/// established, it will answer about, and it says how much is behind every answer.
/// </remarks>
public static class AstrolabeJournalTargets
{
    /// <summary>
    /// The wandering bodies this book can aim at: the ones a conclusion pinned down, and the ones
    /// written down under the older mechanic that recorded no sightings at all.
    /// </summary>
    public static IReadOnlyList<AstrolabeTarget> KnownWanderers(
        IReadOnlyList<AstrolabeTarget> planetTargets,
        ObservationLog observations,
        PlanetJournal planetJournal)
    {
        ArgumentNullException.ThrowIfNull(planetTargets);
        ArgumentNullException.ThrowIfNull(observations);
        ArgumentNullException.ThrowIfNull(planetJournal);

        var known = new List<AstrolabeTarget>();
        foreach (var target in planetTargets)
        {
            if (observations.FindClaimBoundTo(target.SourceId) is { } claim)
            {
                var evidence = observations.EvidenceFor(claim);
                known.Add(target with
                {
                    Provenance = ObservationProvenance.Describe(evidence),
                    Confidence = ObservationProvenance.Grade(evidence)
                });
                continue;
            }

            // Identified under the one-press mechanic, before the book kept sightings. It still
            // names and aims; it just cannot say why, and must not sound as if it can.
            if (planetJournal.IsIdentified(target.SourceId))
            {
                known.Add(target with
                {
                    Provenance = ObservationProvenance.Describe([]),
                    Confidence = ObservationConfidence.Legacy
                });
            }
        }

        return known;
    }

    /// <summary>
    /// The observer's own figures. Nothing is forecast about a fixed star that their drawing does not
    /// already fix, so the evidence is the drawing itself.
    /// </summary>
    public static IReadOnlyList<AstrolabeTarget> Drawn(IReadOnlyList<AstrolabeTarget> constellationTargets)
    {
        ArgumentNullException.ThrowIfNull(constellationTargets);

        return constellationTargets
            .Select(target => target with
            {
                Provenance = $"drawn from {target.StarCount} stars",
                Confidence = ObservationConfidence.Good
            })
            .ToList();
    }

    /// <summary>
    /// The comets. Their returns are somebody else's arithmetic rather than the observer's, and the
    /// instrument says so rather than passing the almanac off as their own work.
    /// </summary>
    public static IReadOnlyList<AstrolabeTarget> FromAlmanac(IReadOnlyList<AstrolabeTarget> cometTargets)
    {
        ArgumentNullException.ThrowIfNull(cometTargets);

        return cometTargets
            .Select(target => target with { Provenance = "from the almanac, not your sightings" })
            .ToList();
    }
}
