using System.Text.Json;

namespace AstraTerra.Observation;

/// <summary>
/// The sightings an observer has written down, in the order they wrote them.
/// </summary>
/// <remarks>
/// This is the primitive the rest of the discovery system is built from. Not discovery, not
/// identification, not naming: <em>I pointed here, at this time, and measured this.</em> A star, a
/// wanderer and a comet all leave entries of the same shape, and they are told apart afterwards by
/// how a set of entries compares — which is how they were told apart in the first place.
/// <para>
/// A ledger, not an accumulator: entries are separate and dated, and nothing is ever overwritten,
/// because two sightings of the same thing on different nights are the whole point.
/// </para>
/// </remarks>
public sealed class ObservationLog
{
    private readonly List<ObservationRecord> observations;
    private long nextId;

    public ObservationLog()
        : this(1, [])
    {
    }

    internal ObservationLog(long nextId, IEnumerable<ObservationRecord> observations)
    {
        this.nextId = nextId;
        this.observations = observations.ToList();
    }

    public IReadOnlyList<ObservationRecord> Observations => observations;

    /// <summary>Writes a sighting down, truncating every angle to what the instrument can read.</summary>
    public ObservationRecord Record(
        double altitudeDeg,
        double azimuthDeg,
        double? visualMagnitude,
        int day,
        double hour,
        double latitudeDeg,
        double resolutionDeg)
    {
        var record = new ObservationRecord(
            nextId++,
            InstrumentResolution.Truncate(altitudeDeg, resolutionDeg),
            InstrumentResolution.Truncate(azimuthDeg, resolutionDeg),
            visualMagnitude,
            day,
            hour,
            latitudeDeg,
            resolutionDeg);
        observations.Add(record);
        return record;
    }

    public bool Remove(long id)
    {
        var record = observations.FirstOrDefault(entry => entry.Id == id);
        return record is not null && observations.Remove(record);
    }

    internal ObservationLogSnapshot ToSnapshot() => new(1, nextId, observations.ToList());

    internal static ObservationLog FromSnapshot(ObservationLogSnapshot snapshot)
    {
        var records = (snapshot.Observations ?? [])
            .Where(record => record is not null && double.IsFinite(record.AltitudeDeg))
            .ToList();
        var nextId = Math.Max(1, snapshot.NextId);
        foreach (var record in records)
        {
            nextId = Math.Max(nextId, record.Id + 1);
        }

        return new ObservationLog(nextId, records);
    }
}

internal sealed record ObservationLogSnapshot(
    int SchemaVersion,
    long NextId,
    IReadOnlyList<ObservationRecord> Observations
);

public static class ObservationLogPersistence
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(ObservationLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return JsonSerializer.Serialize(log.ToSnapshot(), SerializerOptions);
    }

    public static ObservationLog Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<ObservationLogSnapshot>(json, SerializerOptions)
                       ?? throw new InvalidOperationException("Observation log JSON was empty.");
        return ObservationLog.FromSnapshot(snapshot);
    }
}
