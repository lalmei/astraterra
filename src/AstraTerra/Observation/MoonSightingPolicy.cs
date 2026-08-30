namespace AstraTerra.Observation;

/// <summary>Whether Vintage Story's moon is visible enough for a Sextant reading.</summary>
public static class MoonSightingPolicy
{
    /// <summary>
    /// The calendar's phase brightness becomes non-positive around new moon. Requiring a positive
    /// value excludes that invisible face without imposing an arbitrary cutoff on lit crescents.
    /// </summary>
    public const double MinimumPhaseBrightness = 0.0;

    public static bool IsSightable(double altitudeDeg, double phaseBrightness)
        => double.IsFinite(altitudeDeg)
            && double.IsFinite(phaseBrightness)
            && altitudeDeg > 0.0
            && phaseBrightness > MinimumPhaseBrightness;
}
