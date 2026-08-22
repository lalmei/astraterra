namespace AstraTerra.Astronomy;

/// <summary>
/// Where an authored comet is, and how bright it is, at a world time.
/// </summary>
/// <remarks>
/// Satisfies the same <see cref="ISkyEphemeris"/> contract a planet does, so a comet reaches the sky
/// and the instruments down the path that already exists rather than growing a second one.
/// <para>
/// The track is interpolated on the sphere rather than in right ascension and declination, because
/// right ascension wraps: a comet crossing 0h authored as 355° then 5° would be dragged the long way
/// round the sky by a plain numeric blend, sweeping 350 degrees in an evening. Rotating between the
/// two directions takes the short way by construction, and also stops a track near the pole from
/// bunching up.
/// </para>
/// <para>
/// Pure, like the planet ephemeris — world time in, coordinates out — so an authored apparition can
/// be checked without a running game. Wrap it in <see cref="CachedSkyEphemeris"/> before sampling it
/// per frame.
/// </para>
/// </remarks>
public sealed class CometEphemeris : ISkyEphemeris
{
    private readonly CometEntry comet;
    private readonly int daysPerYear;
    private readonly CometPathKeyframe[] path;

    public CometEphemeris(CometEntry comet, int daysPerYear)
    {
        ArgumentNullException.ThrowIfNull(comet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        if (comet.Path is null || comet.Path.Count == 0)
        {
            throw new ArgumentException($"Comet '{comet.Id}' has no authored track.", nameof(comet));
        }

        this.comet = comet;
        this.daysPerYear = daysPerYear;
        path = [.. comet.Path.OrderBy(keyframe => keyframe.Phase)];
    }

    public CometEntry Comet => comet;

    public CometApparition ApparitionAt(double totalDays)
        => CometApparitionSchedule.Read(comet, totalDays, daysPerYear);

    public EquatorialCoordinates PositionAt(double totalDays)
        => PositionAtPhase(ApparitionAt(totalDays).Phase);

    public double MagnitudeAt(double totalDays)
        => CometApparitionSchedule.GetMagnitude(comet, ApparitionAt(totalDays).Closeness);

    /// <summary>Degrees of sky the tail spans right now, zero while the comet is away.</summary>
    public double TailLengthDegAt(double totalDays)
        => CometApparitionSchedule.GetTailLengthDeg(comet, ApparitionAt(totalDays).Closeness);

    /// <summary>
    /// The authored track at a point in the apparition. Phases outside the window hold the end
    /// keyframes: the position of an absent comet is not a question anything should be asking, and
    /// extrapolating the track would answer it with somewhere it never goes.
    /// </summary>
    public EquatorialCoordinates PositionAtPhase(double phase)
    {
        if (path.Length == 1 || phase <= path[0].Phase)
        {
            return ToCoordinates(path[0]);
        }

        if (phase >= path[^1].Phase)
        {
            return ToCoordinates(path[^1]);
        }

        var index = 0;
        while (index < path.Length - 2 && path[index + 1].Phase < phase)
        {
            index++;
        }

        var from = path[index];
        var to = path[index + 1];
        var span = to.Phase - from.Phase;
        var fraction = span <= 0 ? 0.0 : (phase - from.Phase) / span;

        return Slerp(from, to, fraction);
    }

    private static EquatorialCoordinates ToCoordinates(CometPathKeyframe keyframe)
        => new(CelestialMath.NormalizeDegrees(keyframe.RightAscensionDeg), keyframe.DeclinationDeg);

    /// <summary>Rotates from one sky direction to the other along the great circle between them.</summary>
    private static EquatorialCoordinates Slerp(CometPathKeyframe from, CometPathKeyframe to, double fraction)
    {
        var (ax, ay, az) = ToUnitVector(from);
        var (bx, by, bz) = ToUnitVector(to);

        var dot = Math.Clamp((ax * bx) + (ay * by) + (az * bz), -1.0, 1.0);
        var angle = Math.Acos(dot);

        // Two keyframes at essentially the same point leave no axis to rotate about, and the sines
        // below both go to zero. A straight blend is exact in that limit anyway.
        double x, y, z;
        if (angle < 1e-8)
        {
            x = ax + ((bx - ax) * fraction);
            y = ay + ((by - ay) * fraction);
            z = az + ((bz - az) * fraction);
        }
        else
        {
            var sine = Math.Sin(angle);
            var fromWeight = Math.Sin((1.0 - fraction) * angle) / sine;
            var toWeight = Math.Sin(fraction * angle) / sine;
            x = (ax * fromWeight) + (bx * toWeight);
            y = (ay * fromWeight) + (by * toWeight);
            z = (az * fromWeight) + (bz * toWeight);
        }

        var length = Math.Sqrt((x * x) + (y * y) + (z * z));
        if (length <= 0)
        {
            return ToCoordinates(from);
        }

        return new EquatorialCoordinates(
            CelestialMath.NormalizeDegrees(Math.Atan2(y / length, x / length) * 180.0 / Math.PI),
            Math.Asin(Math.Clamp(z / length, -1.0, 1.0)) * 180.0 / Math.PI);
    }

    private static (double X, double Y, double Z) ToUnitVector(CometPathKeyframe keyframe)
    {
        var rightAscension = keyframe.RightAscensionDeg * Math.PI / 180.0;
        var declination = keyframe.DeclinationDeg * Math.PI / 180.0;
        var cosDeclination = Math.Cos(declination);

        return (
            cosDeclination * Math.Cos(rightAscension),
            cosDeclination * Math.Sin(rightAscension),
            Math.Sin(declination));
    }
}
