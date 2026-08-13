namespace AstraTerra.Astronomy;

/// <summary>
/// Samples an ephemeris once a world minute rather than once a frame.
/// </summary>
/// <remarks>
/// An orbit costs real arithmetic — Kepler's equation solved for the body and for the Earth — while
/// a planet shifts by arcminutes over a world hour. Recomputing it every frame buys nothing visible,
/// so the sample is quantized to the world minute, the same trick the astrolabe's sky clock uses for
/// its sunrise search.
/// <para>
/// The world minute is deliberately the whole key. A geocentric position does not depend on where
/// the observer stands, so unlike the sky clock this needs no position term; the observer enters
/// afterwards in <see cref="SkyProjection"/>, which is cheap enough to run per frame.
/// </para>
/// <para>
/// One sample is remembered, which suits a caller asking repeatedly about now. A caller that
/// interleaves times — the astrolabe forecast scrolling ahead while the sky renders the present —
/// should hold its own instance rather than share one, or every call will miss.
/// </para>
/// </remarks>
public sealed class CachedSkyEphemeris : ISkyEphemeris
{
    /// <summary>Samples per world hour. 60 makes a sample a world minute.</summary>
    public const double DefaultSamplesPerWorldHour = 60.0;

    private readonly ISkyEphemeris source;
    private readonly double samplesPerWorldDay;
    private bool hasSample;
    private long sampledStep;
    private EquatorialCoordinates sampledCoordinates;
    private double sampledMagnitude;

    public CachedSkyEphemeris(
        ISkyEphemeris source,
        double hoursPerDay,
        double samplesPerWorldHour = DefaultSamplesPerWorldHour)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        if (samplesPerWorldHour <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(samplesPerWorldHour),
                samplesPerWorldHour,
                "Samples per world hour must be positive.");
        }

        this.source = source;
        samplesPerWorldDay = hoursPerDay * samplesPerWorldHour;
    }

    public EquatorialCoordinates PositionAt(double totalDays) => Sample(totalDays).Coordinates;

    public double MagnitudeAt(double totalDays) => Sample(totalDays).Magnitude;

    /// <summary>
    /// Resolves both halves together, so asking for a position and then a magnitude costs one
    /// evaluation rather than two, and both describe the same instant.
    /// </summary>
    /// <remarks>
    /// The underlying ephemeris is read at the top of the world minute rather than at
    /// <paramref name="totalDays"/> itself, so every caller inside a minute is told the same thing
    /// whoever asked first. The position that costs is a fraction of an arcsecond for a planet.
    /// </remarks>
    private (EquatorialCoordinates Coordinates, double Magnitude) Sample(double totalDays)
    {
        if (!double.IsFinite(totalDays))
        {
            return (source.PositionAt(totalDays), source.MagnitudeAt(totalDays));
        }

        var step = (long)Math.Floor(totalDays * samplesPerWorldDay);
        if (hasSample && step == sampledStep)
        {
            return (sampledCoordinates, sampledMagnitude);
        }

        var sampleDays = step / samplesPerWorldDay;
        sampledCoordinates = source.PositionAt(sampleDays);
        sampledMagnitude = source.MagnitudeAt(sampleDays);
        sampledStep = step;
        hasSample = true;
        return (sampledCoordinates, sampledMagnitude);
    }
}
