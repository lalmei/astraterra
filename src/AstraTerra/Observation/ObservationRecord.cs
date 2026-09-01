namespace AstraTerra.Observation;

/// <summary>
/// One sighting, exactly as the instrument gave it: a direction, a moment, and where the observer
/// stood.
/// </summary>
/// <remarks>
/// The record deliberately says nothing about <em>what</em> was sighted. An observer points at a
/// position, not at an object, and the book must never hand them an identity they have not earned:
/// it writes down something at this angle, on this night, from this latitude. What that something
/// is — a fixed star, a wanderer, a comet — is a conclusion drawn later from how a set of these
/// records compares, and it belongs to the observer.
/// <para>
/// Every angle here is already truncated to <paramref name="ResolutionDeg"/>, and that resolution
/// travels with the record so a later answer can say how good the data behind it was.
/// </para>
/// </remarks>
/// <param name="Id">Order of writing, and how an entry is referred to. Starts at 1.</param>
/// <param name="AltitudeDeg">Angle above the horizon, the number the instrument exists to give.</param>
/// <param name="AzimuthDeg">Clockwise from north, matching <see cref="Astronomy.SightedBody"/>.</param>
/// <param name="VisualMagnitude">
/// How bright it looked. Null when the observer had no scale to judge it against, which is the case
/// for the sun and the moon.
/// </param>
/// <param name="Day">Whole world day of the sighting.</param>
/// <param name="Hour">Hour on the world's universal clock, so entries stay comparable across a journey.</param>
/// <param name="LatitudeDeg">Where the observer stood. Marks taken elsewhere do not compare.</param>
/// <param name="ResolutionDeg">What the instrument reads to. See <see cref="InstrumentResolution"/>.</param>
/// <param name="SiderealAngleDeg">
/// How far the sky had turned when the sighting was taken. Null for an entry written before the
/// book kept it, which can still be read but cannot be compared with another night.
/// </param>
/// <param name="LongitudeDeg">
/// Where east-west the observer stood when the sighting was written. Null on older entries. Together
/// with <paramref name="Hour"/> this pins the moment to the world's single clock.
/// </param>
public sealed record ObservationRecord(
    long Id,
    double AltitudeDeg,
    double AzimuthDeg,
    double? VisualMagnitude,
    int Day,
    double Hour,
    double LatitudeDeg,
    double ResolutionDeg,
    double? SiderealAngleDeg = null,
    double? LongitudeDeg = null
)
{
    /// <summary>
    /// Where this sighting falls among the fixed stars, which is the only frame two nights can be
    /// compared in. Null when the entry predates the book keeping the sky's angle.
    /// </summary>
    public Astronomy.EquatorialCoordinates? ToEquatorial()
        => SiderealAngleDeg is not { } siderealAngleDeg
            ? null
            : Astronomy.CelestialMath.GetEquatorialCoordinates(
                AzimuthDeg,
                AltitudeDeg,
                LatitudeDeg,
                siderealAngleDeg);

    /// <summary>The entry as it reads on the page, to no more digits than the instrument earned.</summary>
    public string Describe()
    {
        var altitude = InstrumentResolution.Format(AltitudeDeg, ResolutionDeg, signed: true);
        var azimuth = InstrumentResolution.Format(AzimuthDeg, ResolutionDeg);
        var brightness = VisualMagnitude is null ? string.Empty : $", mag {VisualMagnitude.Value:0.0}";
        return $"day {Day}, {FormatHour(Hour)} — alt {altitude}, az {azimuth}{brightness}";
    }

    private static string FormatHour(double hour)
    {
        var wholeHours = (int)Math.Floor(hour);
        var minutes = (int)Math.Round((hour - wholeHours) * 60.0);
        if (minutes >= 60)
        {
            wholeHours++;
            minutes -= 60;
        }

        return $"{wholeHours:00}:{minutes:00}";
    }
}
