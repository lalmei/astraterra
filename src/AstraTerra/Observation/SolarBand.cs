using System.Text.Json;

namespace AstraTerra.Observation;

/// <summary>One scratch on the rim: the day, and the notch the sun crossed the horizon at.</summary>
public sealed record SolarMark(int Day, double NotchDeg, SolarEvent Event);

public enum SolarMarkOutcome
{
    /// <summary>The mark went on the rim.</summary>
    Scratched = 0,

    /// <summary>This end of this day is already on the disc. Bronze does not take a mark twice.</summary>
    AlreadyMarked = 1,

    /// <summary>Carried too far from where it was scribed. The disc refuses rather than drifting.</summary>
    WrongPlace = 2
}

public sealed record SolarMarkResult(SolarMarkOutcome Outcome, SolarMark? Mark, string Message);

/// <summary>
/// What one arc of the disc shows: where the notches stop, and what stopping there means.
/// </summary>
/// <param name="EarlyEdgeConfirmed">
/// Whether a later mark has fallen short of that edge. Until one has, the observer has a suspicion
/// rather than a turning point, which is the correct thing for them to have.
/// </param>
public sealed record SolarBandReading(
    SolarEvent Event,
    int MarkCount,
    double NotchDeg,
    double? LowNotchDeg,
    int? LowDay,
    bool LowConfirmed,
    double? HighNotchDeg,
    int? HighDay,
    bool HighConfirmed,
    double? SwingDeg,
    double? LatitudeDeg,
    double? YearDays
)
{
    /// <summary>Both ends found and both seen to turn back: a year has actually been measured.</summary>
    public bool IsComplete => LowConfirmed && HighConfirmed;

    /// <summary>
    /// The bearing the middle of a finished band points at, which is a cardinal direction exactly.
    /// </summary>
    /// <remarks>
    /// The sun sets due west at the equinoxes and swings symmetrically either side of that through
    /// the year, so the point halfway between midsummer's setting place and midwinter's <i>is</i>
    /// due west, at every latitude, however wide the band. The sunrise rim gives due east the same
    /// way. So a disc hands back the cardinal directions as a result rather than needing them as a
    /// setting: nothing here was told which way north lay, and a finished rim knows anyway.
    /// </remarks>
    public double? CardinalNotchDeg
        => IsComplete && LowNotchDeg is { } low && HighNotchDeg is { } high
            ? (low + high) / 2.0
            : null;

    /// <summary>
    /// The next day the sun should stand still, counted on from the last turn the disc caught.
    /// This is the payout that matters to somebody who has never looked up: when to sow.
    /// </summary>
    public int? NextTurnDay(int fromDay)
    {
        if (!IsComplete || YearDays is not { } year || year <= 0)
        {
            return null;
        }

        var halfYear = year / 2.0;
        var lastTurn = Math.Max(LowDay!.Value, HighDay!.Value);
        var turnsSince = Math.Ceiling((fromDay - lastTurn) / halfYear);
        return (int)Math.Round(lastTurn + (Math.Max(turnsSince, 0) * halfYear));
    }

    /// <summary>
    /// What the disc says, without saying more than it knows. It never announces a solstice at the
    /// extreme: at the time, nobody can know an extreme was one. It shows the band, and it names the
    /// year only once the sun has gone out and come back.
    /// </summary>
    public string Describe()
    {
        if (MarkCount == 0)
        {
            return "No marks on this rim yet.";
        }

        var arc = $"{MarkCount} marks between {LowNotchDeg:0.#}° and {HighNotchDeg:0.#}°, on notches of {NotchDeg:0.#}°";
        if (!IsComplete)
        {
            var turned = LowConfirmed || HighConfirmed;
            return turned
                ? $"{arc}. One end has stopped moving; the other has not been found yet."
                : $"{arc}. The band is still widening.";
        }

        var latitude = LatitudeDeg is { } degrees
            ? $"{degrees:0.#}° from the equator"
            : "a latitude this band cannot resolve";
        var cardinal = Event == SolarEvent.Sunset ? "west" : "east";
        var bearing = CardinalNotchDeg is { } middle
            ? $" The middle of this band, at {middle:0.#}°, is due {cardinal}."
            : string.Empty;

        return $"{arc} — a full turn of the year: {YearDays:0.#} days, {latitude}.{bearing}";
    }
}

/// <summary>
/// The bronze disc's record: two arcs of scratches, and the place it was scribed at.
/// </summary>
/// <remarks>
/// An accumulator, not a ledger. It holds no dated entries an observer could read back and no
/// numbers they could transcribe — only where the marks stopped — and that is exactly enough to
/// yield a latitude, the length of the year, and when the seasons turn, from bronze and patience
/// alone. It is the slow road to what the astrolabe answers in three seconds, and it needs neither
/// literacy nor ink.
/// <para>
/// It binds to the place of its first mark. Marks taken materially further away are refused rather
/// than folded in, because a plate that reads wrong can be recut and a scratch cannot be undone.
/// </para>
/// </remarks>
public sealed class SolarBand
{
    private readonly List<SolarMark> marks;

    public SolarBand()
        : this(SolarBandPolicy.ArcNotchDeg)
    {
    }

    public SolarBand(double notchDeg)
        : this(null, null, null, [], notchDeg)
    {
    }

    internal SolarBand(
        double? boundLatitudeDeg,
        int? boundX,
        int? boundZ,
        IEnumerable<SolarMark> marks,
        double notchDeg = SolarBandPolicy.ArcNotchDeg)
    {
        BoundLatitudeDeg = boundLatitudeDeg;
        BoundX = boundX;
        BoundZ = boundZ;
        NotchDeg = notchDeg > 0.0 && double.IsFinite(notchDeg) ? notchDeg : SolarBandPolicy.ArcNotchDeg;
        this.marks = marks.ToList();
    }

    /// <summary>
    /// How wide a notch is on this rim, which is a property of the metal it was scratched into. It
    /// travels with the band rather than with the item: a disc carries its own scale, so marks made
    /// on it stay comparable to each other for as long as it exists.
    /// </summary>
    public double NotchDeg { get; }

    /// <summary>The latitude of the first mark. Every later mark is measured against it.</summary>
    public double? BoundLatitudeDeg { get; private set; }

    public int? BoundX { get; private set; }

    public int? BoundZ { get; private set; }

    public IReadOnlyList<SolarMark> Marks => marks;

    public bool IsBound => BoundLatitudeDeg is not null;

    /// <summary>Scratches where the sun crossed the horizon today, if this disc will take the mark.</summary>
    public SolarMarkResult Scratch(int day, double azimuthDeg, SolarEvent solarEvent, double latitudeDeg, int x, int z)
    {
        if (BoundLatitudeDeg is { } bound
            && Math.Abs(latitudeDeg - bound) > SolarBandPolicy.MaxDriftDegFor(NotchDeg))
        {
            return new SolarMarkResult(
                SolarMarkOutcome.WrongPlace,
                null,
                "The sun does not set where this disc was scribed. Carry it home, or scribe another.");
        }

        if (marks.Any(mark => mark.Day == day && mark.Event == solarEvent))
        {
            return new SolarMarkResult(
                SolarMarkOutcome.AlreadyMarked,
                null,
                "This day is already scratched on that rim.");
        }

        BoundLatitudeDeg ??= latitudeDeg;
        BoundX ??= x;
        BoundZ ??= z;

        var mark = new SolarMark(day, SolarBandPolicy.Notch(azimuthDeg, NotchDeg), solarEvent);
        marks.Add(mark);
        return new SolarMarkResult(SolarMarkOutcome.Scratched, mark, $"Scratched at {mark.NotchDeg:0.#}°.");
    }

    /// <summary>What one arc of the rim shows.</summary>
    public SolarBandReading Read(SolarEvent solarEvent)
    {
        var arc = marks.Where(mark => mark.Event == solarEvent).OrderBy(mark => mark.Day).ToList();
        if (arc.Count == 0)
        {
            return new SolarBandReading(solarEvent, 0, NotchDeg, null, null, false, null, null, false, null, null, null);
        }

        // An edge becomes a turning point only when the marks approached it and then fell back:
        // short of it before, short of it after. An edge that is merely where the observer started
        // watching is not a turn, and the disc must not mistake the two — which is also why it
        // cannot announce anything on the day it happens. Several days can share an extreme notch,
        // and the first mark on a new disc may be one of them. Pick the first occurrence that really
        // has an approach and retreat around it; otherwise that unlucky first scratch would shadow
        // the same edge when it comes round next year and the rim could never finish.
        var lowNotch = arc.Min(mark => mark.NotchDeg);
        var highNotch = arc.Max(mark => mark.NotchDeg);
        var low = FindEdge(
            arc,
            lowNotch,
            mark => mark.NotchDeg >= lowNotch + NotchDeg,
            out var lowConfirmed);
        var high = FindEdge(
            arc,
            highNotch,
            mark => mark.NotchDeg <= highNotch - NotchDeg,
            out var highConfirmed);

        var complete = lowConfirmed && highConfirmed;
        var swing = complete ? high.NotchDeg - low.NotchDeg : (double?)null;
        var latitude = swing is { } width ? SolarBandPolicy.LatitudeFromSwingDeg(width) : null;

        // Solstice to solstice is half a year, so the two ends of the band measure the whole of it.
        var year = complete ? Math.Abs(high.Day - low.Day) * 2.0 : (double?)null;

        return new SolarBandReading(
            solarEvent,
            arc.Count,
            NotchDeg,
            low.NotchDeg,
            low.Day,
            lowConfirmed,
            high.NotchDeg,
            high.Day,
            highConfirmed,
            swing,
            latitude,
            year is > 0 ? year : null);
    }

    private static bool Turned(IReadOnlyList<SolarMark> arc, SolarMark edge, Func<SolarMark, bool> fallsShort)
        => arc.Any(mark => mark.Day < edge.Day && fallsShort(mark))
           && arc.Any(mark => mark.Day > edge.Day && fallsShort(mark));

    private static SolarMark FindEdge(
        IReadOnlyList<SolarMark> arc,
        double edgeNotchDeg,
        Func<SolarMark, bool> fallsShort,
        out bool confirmed)
    {
        var candidates = arc.Where(mark => mark.NotchDeg == edgeNotchDeg).OrderBy(mark => mark.Day).ToList();
        var turned = candidates.FirstOrDefault(candidate => Turned(arc, candidate, fallsShort));
        confirmed = turned is not null;
        return turned ?? candidates[0];
    }

    internal SolarBandSnapshot ToSnapshot() => new(2, BoundLatitudeDeg, BoundX, BoundZ, marks.ToList(), NotchDeg);

    internal static SolarBand FromSnapshot(SolarBandSnapshot snapshot)
        => new(
            snapshot.BoundLatitudeDeg,
            snapshot.BoundX,
            snapshot.BoundZ,
            (snapshot.Marks ?? []).Where(mark => mark is not null && double.IsFinite(mark.NotchDeg)),
            // A disc scratched before rims came in more than one metal is a bronze one.
            snapshot.NotchDeg ?? SolarBandPolicy.ArcNotchDeg);
}

internal sealed record SolarBandSnapshot(
    int SchemaVersion,
    double? BoundLatitudeDeg,
    int? BoundX,
    int? BoundZ,
    IReadOnlyList<SolarMark> Marks,
    double? NotchDeg = null
);

public static class SolarBandPersistence
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(SolarBand band)
    {
        ArgumentNullException.ThrowIfNull(band);

        return JsonSerializer.Serialize(band.ToSnapshot(), SerializerOptions);
    }

    public static SolarBand Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<SolarBandSnapshot>(json, SerializerOptions)
                       ?? throw new InvalidOperationException("Sky disc JSON was empty.");
        return SolarBand.FromSnapshot(snapshot);
    }
}
