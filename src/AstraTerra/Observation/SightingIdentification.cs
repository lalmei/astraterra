using AstraTerra.Astronomy;

namespace AstraTerra.Observation;

/// <summary>
/// Which body in the sky a set of sightings was actually of, worked out from the sightings alone.
/// </summary>
/// <remarks>
/// This is not the game handing over an answer. The observer has already done the discovering: they
/// pointed, they came back, they compared, and they said it wanders. All this does is look up which
/// of the wandering bodies was in those places on those nights, so that instruments can work from
/// the observer's record instead of from a catalog they were never shown.
/// <para>
/// It answers only when one candidate fits <em>every</em> entry. Two bodies that both fit, or none
/// that do, leave the conclusion unbound — the observer's name for it still stands, and the
/// instruments simply have nothing to aim with yet. Being unable to say is a real answer here.
/// </para>
/// </remarks>
public static class SightingIdentification
{
    /// <summary>
    /// How far a candidate may sit from a sighting and still be taken for it, when the instrument
    /// itself was finer than that. Covers the slack in reading an angle off a scale at all.
    /// </summary>
    public const double ToleranceFloorDeg = 0.25;

    /// <summary>
    /// The one candidate whose track passes through every sighting, or null when the record cannot
    /// tell them apart.
    /// </summary>
    /// <param name="hoursPerDay">The world's day length, which turns an entry's hour into world time.</param>
    public static string? Match(
        IEnumerable<ObservationRecord> records,
        IReadOnlyList<(string Id, ISkyEphemeris Ephemeris)> candidates,
        double hoursPerDay)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(candidates);

        var placed = records
            .Select(record => (Record: record, Position: record.ToEquatorial()))
            .Where(entry => entry.Position is not null)
            .Select(entry => (entry.Record, Position: entry.Position!.Value))
            .ToList();
        if (placed.Count == 0 || candidates.Count == 0 || hoursPerDay <= 0)
        {
            return null;
        }

        var fits = candidates
            .Where(candidate => placed.All(entry => Fits(candidate.Ephemeris, entry.Record, entry.Position, hoursPerDay)))
            .Select(candidate => candidate.Id)
            .Take(2)
            .ToList();

        return fits.Count == 1 ? fits[0] : null;
    }

    private static bool Fits(
        ISkyEphemeris ephemeris,
        ObservationRecord record,
        EquatorialCoordinates sighted,
        double hoursPerDay)
    {
        var totalDays = record.Day + (record.Hour / hoursPerDay);
        var separation = Separation(ephemeris.PositionAt(totalDays), sighted);
        return separation <= Math.Max(record.ResolutionDeg * 4.0, ToleranceFloorDeg);
    }

    private static double Separation(EquatorialCoordinates a, EquatorialCoordinates b)
    {
        var declinationA = a.DeclinationDeg * Math.PI / 180.0;
        var declinationB = b.DeclinationDeg * Math.PI / 180.0;
        var deltaRightAscension = CelestialMath.ShortestAngularDistanceDegrees(
            a.RightAscensionDeg,
            b.RightAscensionDeg) * Math.PI / 180.0;

        var cosine = (Math.Sin(declinationA) * Math.Sin(declinationB))
                     + (Math.Cos(declinationA) * Math.Cos(declinationB) * Math.Cos(deltaRightAscension));
        return Math.Acos(Math.Clamp(cosine, -1.0, 1.0)) * 180.0 / Math.PI;
    }
}
