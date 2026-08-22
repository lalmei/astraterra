namespace AstraTerra.Astronomy;

/// <summary>
/// When a comet is here, how far through its apparition it is, and how strongly it is showing.
/// </summary>
/// <remarks>
/// Everything about a comet that changes with time comes from one number: <see cref="Phase"/>, which
/// runs from -1 as the comet is picked up, through 0 at perihelion, to +1 as it is lost. Brightness
/// and tail length are both read off the same curve, so an apparition builds and fades as one thing
/// rather than as a coma and a tail that happen to be animated in parallel.
/// </remarks>
/// <param name="Phase">-1 to +1 inside the window; larger in magnitude when the comet is away.</param>
/// <param name="PerihelionWorldYear">The passage this phase is measured against.</param>
/// <param name="Closeness">1 at perihelion, 0 at the edges of the window, and 0 while away.</param>
public readonly record struct CometApparition(
    double Phase,
    double PerihelionWorldYear,
    double Closeness)
{
    public bool IsVisible => Math.Abs(Phase) <= 1.0;
}

/// <summary>
/// The recurrence maths: which passage a world time falls nearest, how far through the apparition it
/// is, and how long until the next one begins.
/// </summary>
public static class CometApparitionSchedule
{
    /// <summary>World years elapsed, which is the clock every comet is authored against.</summary>
    public static double WorldYears(double totalDays, int daysPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);

        return totalDays / daysPerYear;
    }

    /// <summary>The perihelion passage nearest a world time, which may be behind or ahead of it.</summary>
    public static double NearestPerihelionYear(CometEntry comet, double worldYears)
    {
        ArgumentNullException.ThrowIfNull(comet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(comet.PeriodYears);

        var passages = Math.Round((worldYears - comet.FirstPerihelionYear) / comet.PeriodYears);
        return comet.FirstPerihelionYear + (passages * comet.PeriodYears);
    }

    public static CometApparition Read(CometEntry comet, double worldYears)
    {
        ArgumentNullException.ThrowIfNull(comet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(comet.WindowHalfWidthYears);

        var perihelion = NearestPerihelionYear(comet, worldYears);
        var phase = (worldYears - perihelion) / comet.WindowHalfWidthYears;
        return new CometApparition(phase, perihelion, GetCloseness(comet, phase));
    }

    public static CometApparition Read(CometEntry comet, double totalDays, int daysPerYear)
        => Read(comet, WorldYears(totalDays, daysPerYear));

    /// <summary>
    /// How strongly the comet is showing: 1 at perihelion, 0 at the edges of the window and beyond.
    /// </summary>
    /// <remarks>
    /// <see cref="CometEntry.BrighteningExponent"/> below 1 keeps this near zero across most of the
    /// window and lifts it sharply near perihelion, which is what makes an apparition read as an
    /// event — the comet is a faint smudge for a week and a half, then briefly the most interesting
    /// thing in the sky, then a smudge again.
    /// </remarks>
    public static double GetCloseness(CometEntry comet, double phase)
    {
        ArgumentNullException.ThrowIfNull(comet);

        var distance = Math.Abs(phase);
        if (distance >= 1.0)
        {
            return 0.0;
        }

        return 1.0 - Math.Pow(distance, Math.Max(0.01, comet.BrighteningExponent));
    }

    /// <summary>Visual magnitude for a closeness, brightest at perihelion.</summary>
    public static double GetMagnitude(CometEntry comet, double closeness)
    {
        ArgumentNullException.ThrowIfNull(comet);

        return comet.EdgeMagnitude + ((comet.PeakMagnitude - comet.EdgeMagnitude) * Math.Clamp(closeness, 0.0, 1.0));
    }

    /// <summary>Degrees of sky the tail spans, off the same curve as the brightness.</summary>
    public static double GetTailLengthDeg(CometEntry comet, double closeness)
    {
        ArgumentNullException.ThrowIfNull(comet);

        return Math.Max(0.0, comet.PeakTailLengthDeg * Math.Clamp(closeness, 0.0, 1.0));
    }

    /// <summary>
    /// The world year the next apparition begins, for a comet that is not here now.
    /// </summary>
    public static double NextWindowOpenYear(CometEntry comet, double worldYears)
    {
        ArgumentNullException.ThrowIfNull(comet);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(comet.PeriodYears);

        var passages = Math.Floor(
            (worldYears - comet.FirstPerihelionYear + comet.WindowHalfWidthYears) / comet.PeriodYears);

        // Walks forward rather than trusting the division outright: the floor can land a passage
        // short or long at the exact moment a window closes, and a comet reported as returning in
        // "-3 days" would be worse than one reported a period late.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var opensAt = comet.FirstPerihelionYear + (passages * comet.PeriodYears) - comet.WindowHalfWidthYears;
            if (opensAt > worldYears)
            {
                return opensAt;
            }

            passages += 1.0;
        }

        return comet.FirstPerihelionYear + (passages * comet.PeriodYears) - comet.WindowHalfWidthYears;
    }

    /// <summary>
    /// World days until the comet can next be seen, or zero while it is already up.
    /// </summary>
    /// <remarks>
    /// This is what makes a comet worth an instrument. A body that is simply absent teaches a player
    /// nothing; one the astrolabe says is 340 days out is something to plan around.
    /// </remarks>
    public static double DaysUntilVisible(CometEntry comet, double totalDays, int daysPerYear)
    {
        ArgumentNullException.ThrowIfNull(comet);

        var worldYears = WorldYears(totalDays, daysPerYear);
        if (Read(comet, worldYears).IsVisible)
        {
            return 0.0;
        }

        return Math.Max(0.0, (NextWindowOpenYear(comet, worldYears) - worldYears) * daysPerYear);
    }
}
