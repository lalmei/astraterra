using AstraTerra.Astronomy;

namespace AstraTerra.Observation;

/// <summary>
/// Sightings the observer took to be of the same thing, and what comparing them shows.
/// </summary>
/// <param name="Number">Position in the listing, so an observer can point at a group and say what it is.</param>
public sealed record SightingGroup(int Number, IReadOnlyList<ObservationRecord> Records)
{
    public ObservationRecord First => Records[0];

    public ObservationRecord Last => Records[^1];

    /// <summary>Nights between the first and last sighting, which is what any rate is measured over.</summary>
    public double DaysSpan => (Last.Day + (Last.Hour / 24.0)) - (First.Day + (First.Hour / 24.0));

    /// <summary>How far it has moved among the fixed stars since the first sighting.</summary>
    public double SeparationDeg => SightingComparison.SeparationDeg(First, Last) ?? 0.0;

    /// <summary>The coarsest instrument in the set: a comparison is only as good as its worst reading.</summary>
    public double ResolutionDeg => Records.Max(record => record.ResolutionDeg);

    /// <summary>
    /// The smallest displacement this set could prove. Two readings each truncated in altitude and
    /// in bearing can differ by more than one graduation without anything having moved, so the bar
    /// sits at twice the coarsest scale in the set.
    /// </summary>
    public double DetectionThresholdDeg => ResolutionDeg * 2.0;

    /// <summary>
    /// Whether the displacement is larger than the instrument could have invented. Below this the
    /// honest answer is that it has not moved <em>as far as this instrument can tell</em>, which is
    /// why a crude one needs more nights rather than a better excuse.
    /// </summary>
    public bool MovedBeyondResolution => Records.Count > 1 && SeparationDeg > DetectionThresholdDeg;

    public double DegreesPerDay => Math.Abs(DaysSpan) < 1e-6 ? 0.0 : SeparationDeg / DaysSpan;

    /// <summary>What the comparison shows, in measurements only. What it <em>is</em> is not ours to say.</summary>
    public string Describe()
    {
        var entries = string.Join(", ", Records.Select(record => $"#{record.Id}"));
        if (Records.Count == 1)
        {
            return $"{entries} — one sighting, nothing yet to compare it with";
        }

        if (First.SiderealAngleDeg is null || Last.SiderealAngleDeg is null)
        {
            return $"{entries} — {Records.Count} sightings, written before the book kept the sky's angle";
        }

        var span = $"over {DaysSpan:0.#} days";
        return MovedBeyondResolution
            ? $"{entries} — moved {SeparationDeg:0.##}° {span}, {DegreesPerDay:0.###}°/day"
            : $"{entries} — held its place {span}, to within {InstrumentResolution.Format(DetectionThresholdDeg, ResolutionDeg)}";
    }
}

/// <summary>
/// The arithmetic the book does on a ledger of sightings, so that the observer never has to.
/// </summary>
/// <remarks>
/// The book supplies patience-free arithmetic and nothing else: it says how far apart two entries
/// are and how fast that is per day. It never says what the thing was. Deciding that a body which
/// held its place is a fixed star, and one that did not is a wanderer, is the observer's call, and
/// recording that call is what <see cref="ObservationLog.Claim"/> is for.
/// </remarks>
public static class SightingComparison
{
    /// <summary>
    /// How far a body is allowed to have moved per day and still be taken for the same body on a
    /// later night. Generous on purpose: it has to cover the fastest wanderer without needing to
    /// know which body it is looking at, and an observer who lumps two things together finds out by
    /// the numbers refusing to make sense.
    /// </summary>
    public const double DefaultMaxDriftDegPerDay = 3.0;

    /// <summary>Angular separation among the fixed stars, or null when either entry cannot be placed there.</summary>
    public static double? SeparationDeg(ObservationRecord from, ObservationRecord to)
    {
        if (from.ToEquatorial() is not { } a || to.ToEquatorial() is not { } b)
        {
            return null;
        }

        var declinationA = ToRadians(a.DeclinationDeg);
        var declinationB = ToRadians(b.DeclinationDeg);
        var deltaRightAscension = ToRadians(
            CelestialMath.ShortestAngularDistanceDegrees(a.RightAscensionDeg, b.RightAscensionDeg));

        var cosine = (Math.Sin(declinationA) * Math.Sin(declinationB))
                     + (Math.Cos(declinationA) * Math.Cos(declinationB) * Math.Cos(deltaRightAscension));
        return ToDegrees(Math.Acos(Math.Clamp(cosine, -1.0, 1.0)));
    }

    /// <summary>
    /// Gathers the ledger into the sets an observer would take to be one body: each entry joins the
    /// most recent set it could plausibly belong to, allowing for how far anything could have
    /// drifted in the nights between.
    /// </summary>
    public static IReadOnlyList<SightingGroup> Group(
        IEnumerable<ObservationRecord> records,
        double maxDriftDegPerDay = DefaultMaxDriftDegPerDay)
    {
        ArgumentNullException.ThrowIfNull(records);

        var groups = new List<List<ObservationRecord>>();
        foreach (var record in records.OrderBy(record => record.Id))
        {
            // Nearest wins, not first: when two sets could both have drifted this far, the entry
            // belongs with whichever it actually landed closest to.
            var target = groups
                .Select(group => (Group: group, Separation: SeparationDeg(group[^1], record)))
                .Where(candidate => Accepts(candidate.Group[^1], record, maxDriftDegPerDay))
                .OrderBy(candidate => candidate.Separation)
                .Select(candidate => candidate.Group)
                .FirstOrDefault();
            if (target is null)
            {
                groups.Add([record]);
                continue;
            }

            target.Add(record);
        }

        return groups
            .Select((group, index) => new SightingGroup(index + 1, group))
            .ToList();
    }

    private static bool Accepts(ObservationRecord latest, ObservationRecord candidate, double maxDriftDegPerDay)
    {
        if (SeparationDeg(latest, candidate) is not { } separation)
        {
            // An entry with no sky angle cannot be placed among the stars, so it can only ever
            // stand alone. Pretending otherwise would invent a comparison the book cannot make.
            return false;
        }

        var days = Math.Abs((candidate.Day + (candidate.Hour / 24.0)) - (latest.Day + (latest.Hour / 24.0)));
        var tolerance = Math.Max(
            (latest.ResolutionDeg + candidate.ResolutionDeg) * 2.0,
            maxDriftDegPerDay * days);
        return separation <= tolerance;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
