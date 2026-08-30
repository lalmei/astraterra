namespace AstraTerra.Observation;

/// <summary>
/// Which of the bodies under the sight the instrument is actually on.
/// </summary>
/// <remarks>
/// Two different questions, because the sky holds two different kinds of thing. A star, a planet or
/// a comet is a point: the sight is on it when it falls inside the reticle, and of two points inside
/// the reticle the nearer one wins. A near body is a disc tens of degrees wide: the sight is on it
/// whenever the crosshair is anywhere on that disc, which is a far larger area than the reticle, and
/// of two overlapping discs the smaller one wins — a sibling moon crossing in front of the parent
/// giant is what the observer is looking at, not the giant behind it.
/// <para>
/// A disc beats a point outright. The near-body pass draws inside the star sphere, so a star whose
/// place on the sky lies behind a disc is not visible at all; picking it would be measuring
/// something the observer cannot see.
/// </para>
/// </remarks>
public static class SightingTargetPolicy
{
    /// <summary>How far off centre a point body may sit and still be taken as sighted.</summary>
    public const double ReticleRadiusPixels = 44.0;

    /// <summary>A candidate's standing, lowest first. Null when the sight is not on it at all.</summary>
    /// <param name="Tier">0 for a disc under the crosshair, 1 for a point inside the reticle.</param>
    /// <param name="Score">Disc radius within tier 0, distance from centre within tier 1.</param>
    public readonly record struct TargetRank(int Tier, double Score) : IComparable<TargetRank>
    {
        public int CompareTo(TargetRank other)
        {
            var tier = Tier.CompareTo(other.Tier);
            return tier != 0 ? tier : Score.CompareTo(other.Score);
        }
    }

    /// <param name="distancePixels">How far the body's centre is drawn from the centre of the screen.</param>
    /// <param name="discRadiusPixels">The body's own radius on screen, or zero for a point body.</param>
    public static TargetRank? Rank(
        double distancePixels,
        double discRadiusPixels,
        double reticleRadiusPixels = ReticleRadiusPixels)
    {
        if (!double.IsFinite(distancePixels) || distancePixels < 0.0)
        {
            return null;
        }

        if (discRadiusPixels > 0.0 && distancePixels <= discRadiusPixels)
        {
            return new TargetRank(0, discRadiusPixels);
        }

        return distancePixels <= reticleRadiusPixels ? new TargetRank(1, distancePixels) : null;
    }

    /// <summary>
    /// How many pixels a degree covers at the centre of the screen, from the projection matrix's
    /// vertical focal term (<c>1 / tan(fovY / 2)</c>).
    /// </summary>
    /// <remarks>
    /// Exact on the optical axis and a little tight away from it, since the screen is a plane and
    /// the sky is not. That only matters for deciding whether the crosshair is on a disc, which is a
    /// question asked near the centre of the screen by definition, so the error stays well inside
    /// the width of the reticle.
    /// </remarks>
    public static double PixelsPerDegree(double verticalFocalTerm, double frameHeightPixels)
    {
        if (!double.IsFinite(verticalFocalTerm) || verticalFocalTerm <= 0.0 || frameHeightPixels <= 0.0)
        {
            return 0.0;
        }

        return frameHeightPixels * 0.5 * verticalFocalTerm * Math.PI / 180.0;
    }
}
