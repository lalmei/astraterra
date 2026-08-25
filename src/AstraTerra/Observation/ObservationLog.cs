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
    private readonly List<SightingClaim> claims;
    private long nextId;
    private long nextClaimId;

    public ObservationLog()
        : this(1, [], 1, [])
    {
    }

    internal ObservationLog(
        long nextId,
        IEnumerable<ObservationRecord> observations,
        long nextClaimId,
        IEnumerable<SightingClaim> claims)
    {
        this.nextId = nextId;
        this.observations = observations.ToList();
        this.nextClaimId = nextClaimId;
        this.claims = claims.ToList();
    }

    public IReadOnlyList<ObservationRecord> Observations => observations;

    /// <summary>What the observer has concluded, which is a separate thing from what they measured.</summary>
    public IReadOnlyList<SightingClaim> Claims => claims;

    /// <summary>Writes a sighting down, truncating every angle to what the instrument can read.</summary>
    public ObservationRecord Record(
        double altitudeDeg,
        double azimuthDeg,
        double? visualMagnitude,
        int day,
        double hour,
        double latitudeDeg,
        double resolutionDeg,
        double? siderealAngleDeg = null)
    {
        var record = new ObservationRecord(
            nextId++,
            InstrumentResolution.Truncate(altitudeDeg, resolutionDeg),
            InstrumentResolution.Truncate(azimuthDeg, resolutionDeg),
            visualMagnitude,
            day,
            hour,
            latitudeDeg,
            resolutionDeg,
            siderealAngleDeg);
        observations.Add(record);
        return record;
    }

    /// <summary>
    /// Writes down what the observer says a set of sightings was.
    /// </summary>
    /// <remarks>
    /// A conclusion replaces any earlier conclusion that rested on the same entries, because
    /// changing your mind about what you saw is the whole exercise — the entries themselves are
    /// never touched, so the evidence outlives every reading of it.
    /// </remarks>
    public SightingClaim Claim(SkyClass skyClass, string? name, IEnumerable<long> recordIds, int day)
    {
        ArgumentNullException.ThrowIfNull(recordIds);

        var known = recordIds
            .Distinct()
            .Where(id => observations.Any(record => record.Id == id))
            .OrderBy(id => id)
            .ToList();
        if (known.Count == 0)
        {
            throw new InvalidOperationException("No such sighting to draw a conclusion from.");
        }

        claims.RemoveAll(claim => claim.RecordIds.Any(known.Contains));
        var record = new SightingClaim(
            nextClaimId++,
            skyClass,
            string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            known,
            day);
        claims.Add(record);
        return record;
    }

    /// <summary>What the observer concluded about an entry, if they have concluded anything yet.</summary>
    public SightingClaim? FindClaimFor(long recordId)
        => claims.FirstOrDefault(claim => claim.RecordIds.Contains(recordId));

    public bool RemoveClaim(long claimId)
    {
        var claim = claims.FirstOrDefault(entry => entry.Id == claimId);
        return claim is not null && claims.Remove(claim);
    }

    public bool Remove(long id)
    {
        var record = observations.FirstOrDefault(entry => entry.Id == id);
        return record is not null && observations.Remove(record);
    }

    internal ObservationLogSnapshot ToSnapshot()
        => new(1, nextId, observations.ToList(), nextClaimId, claims.ToList());

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

        // A conclusion about an entry that is no longer there has nothing left to rest on.
        var conclusions = (snapshot.Claims ?? [])
            .Where(claim => claim is not null && claim.RecordIds is not null)
            .Select(claim => claim with
            {
                RecordIds = claim.RecordIds.Where(id => records.Any(record => record.Id == id)).ToList()
            })
            .Where(claim => claim.RecordIds.Count > 0)
            .ToList();
        var nextClaimId = Math.Max(1, snapshot.NextClaimId);
        foreach (var claim in conclusions)
        {
            nextClaimId = Math.Max(nextClaimId, claim.Id + 1);
        }

        return new ObservationLog(nextId, records, nextClaimId, conclusions);
    }
}

internal sealed record ObservationLogSnapshot(
    int SchemaVersion,
    long NextId,
    IReadOnlyList<ObservationRecord> Observations,
    long NextClaimId = 1,
    IReadOnlyList<SightingClaim>? Claims = null
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
