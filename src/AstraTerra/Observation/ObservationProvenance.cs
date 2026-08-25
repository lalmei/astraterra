namespace AstraTerra.Observation;

/// <summary>
/// How much an answer derived from the journal is worth.
/// </summary>
/// <remarks>
/// Every derived answer carries one of these, because an instrument reading a thin record and an
/// instrument reading a thick one must not sound alike. A player who cannot see that a forecast rests
/// on a single sighting will read its errors as a bug and report it as one — and they would be right
/// to, because an answer that hides its own thinness is broken whatever its numbers say.
/// </remarks>
public enum ObservationConfidence
{
    /// <summary>Nothing was measured. Whatever is being answered rests on air.</summary>
    None = 0,

    /// <summary>
    /// Written down before the book kept sightings, so it names and aims but cannot say why.
    /// Re-observing it is what turns it into evidence.
    /// </summary>
    Legacy = 1,

    /// <summary>One night. Enough to point at, not enough to predict from.</summary>
    Low = 2,

    /// <summary>Two nights: a direction of travel, and a rate with one measurement behind it.</summary>
    Fair = 3,

    /// <summary>Several nights across a real span. A rate worth extrapolating.</summary>
    Good = 4
}

/// <summary>
/// Turns the sightings behind a conclusion into a line an instrument can print beside its answer.
/// </summary>
public static class ObservationProvenance
{
    /// <summary>Nights an answer must span before its rate is worth extrapolating.</summary>
    public const double GoodSpanDays = 2.0;

    public static ObservationConfidence Grade(IReadOnlyList<ObservationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return records.Count switch
        {
            0 => ObservationConfidence.None,
            1 => ObservationConfidence.Low,
            2 => ObservationConfidence.Fair,
            _ => SpanDays(records) >= GoodSpanDays ? ObservationConfidence.Good : ObservationConfidence.Fair
        };
    }

    /// <summary>What is behind the answer, in the observer's own terms: how many nights, and how fine.</summary>
    public static string Describe(IReadOnlyList<ObservationRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return "recorded without sightings — low confidence until re-observed";
        }

        var sightings = records.Count == 1 ? "1 sighting" : $"{records.Count} sightings";
        var span = SpanDays(records);
        var scale = InstrumentResolution.Format(
            records.Max(record => record.ResolutionDeg),
            records.Max(record => record.ResolutionDeg));

        return span >= 1.0
            ? $"from {sightings} over {span:0.#} days, to {scale}"
            : $"from {sightings}, to {scale}";
    }

    public static string Name(ObservationConfidence confidence)
        => confidence switch
        {
            ObservationConfidence.Good => "good",
            ObservationConfidence.Fair => "fair",
            ObservationConfidence.Low => "low",
            ObservationConfidence.Legacy => "unrecorded",
            _ => "none"
        };

    private static double SpanDays(IReadOnlyList<ObservationRecord> records)
    {
        var moments = records.Select(record => record.Day + (record.Hour / 24.0)).ToList();
        return moments.Max() - moments.Min();
    }
}
