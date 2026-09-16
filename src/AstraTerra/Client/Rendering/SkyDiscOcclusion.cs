using AstraTerra.Astronomy;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// A globe drawn in front of the star field: where it is, how wide it is, and how much of it is
/// really there to block anything.
/// </summary>
/// <param name="Coverage">
/// How completely this globe blocks what is behind it, 1 for one properly up and 0 for one the
/// horizon haze has already taken. Deliberately not how solidly it is drawn: a daytime moon is
/// washed out to almost nothing against a bright sky and still blocks every star behind it.
/// </param>
public readonly record struct SkyOccultingDisc(
    SkyDirection Direction,
    double AngularDiameterDeg,
    double Coverage = 1.0);

/// <summary>One occulting globe reduced to what the star loop needs: a centre and two cosines.</summary>
public readonly record struct SkyOccultingField(
    double CenterX,
    double CenterY,
    double CenterZ,
    double FullCoverCos,
    double EdgeCos,
    double Coverage);

/// <summary>
/// What the discs in front of the star field do to the stars behind them.
/// </summary>
/// <remarks>
/// <para>
/// The sky is painted, not depth-sorted: every pass here draws with the depth test off, so what is
/// in front is whatever was drawn last. That works only while the thing in front is opaque, and the
/// discs in this sky are not always. A near body fades into the daylight and into the horizon haze,
/// the moon crossfades between a solid night disc and a daylight glow, and Vintage Story's own moon
/// is drawn before this mod's star pass runs at all. In every one of those cases the stars behind
/// the globe are mixed back into it, and a moon reads as a hole cut in a window rather than a rock.
/// </para>
/// <para>
/// So the stars behind a globe are not drawn. A star is a point of light, and a globe either passes
/// it or does not; taking it out of the batch is exactly the occultation, and it costs nothing at
/// draw time because it is done where the batch is built. This is the same rule
/// <see cref="NearBodyMeshBuilder"/> already applies between one near body and the next -- alpha
/// erases nothing, so what is hidden is not drawn -- extended to the star field those bodies hang
/// in front of.
/// </para>
/// <para>
/// Shaped like <see cref="DeepSkyPlateVisibility"/> and used in the same loop, but answering a
/// different question: a plate is a photograph that should out-argue the catalog's own stars, while
/// this is rock in the way. That is why a globe's edge is nearly hard where a plate's field thins
/// out over half its radius.
/// </para>
/// </remarks>
public static class SkyDiscOcclusion
{
    /// <summary>
    /// The share of a globe's radius that blocks completely, beyond which it ramps off to nothing
    /// at the limb.
    /// </summary>
    /// <remarks>
    /// A globe's limb is a hard edge and this is nearly one. The sliver of ramp is not physics: the
    /// star batch is rebuilt in steps as the sky turns, so a star crossing a perfectly hard edge
    /// would blink out between one rebuild and the next. Over a fraction of a degree it fades
    /// instead, which is also what a real occultation looks like through air.
    /// </remarks>
    public const double FullCoverShare = 0.94;

    /// <summary>
    /// Reduces the discs to what the star loop needs, once per rebuild rather than once per star.
    /// </summary>
    public static void BuildFields(
        IReadOnlyList<SkyOccultingDisc> discs,
        List<SkyOccultingField> destination)
    {
        ArgumentNullException.ThrowIfNull(discs);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        for (var index = 0; index < discs.Count; index++)
        {
            var disc = discs[index];
            if (disc.AngularDiameterDeg <= 0)
            {
                continue;
            }

            var coverage = Math.Clamp(disc.Coverage, 0.0, 1.0);
            if (coverage <= 0.001)
            {
                continue;
            }

            var direction = disc.Direction;
            var length = Math.Sqrt(
                (direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));
            if (length < 1e-9)
            {
                continue;
            }

            var edgeRad = disc.AngularDiameterDeg * Math.PI / 360.0;

            // Cosines rather than angles, so the star loop compares a dot product and never calls an
            // inverse trigonometric function.
            destination.Add(new SkyOccultingField(
                direction.X / length,
                direction.Y / length,
                direction.Z / length,
                Math.Cos(edgeRad * FullCoverShare),
                Math.Cos(edgeRad),
                coverage));
        }
    }

    /// <summary>
    /// How much of its light a star keeps where it lies, which is all of it unless a globe stands
    /// in front of that part of the sky.
    /// </summary>
    public static double GetStarVisibilityFactor(
        IReadOnlyList<SkyOccultingField> fields,
        double directionX,
        double directionY,
        double directionZ)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var factor = 1.0;
        for (var index = 0; index < fields.Count; index++)
        {
            var field = fields[index];
            var cosine = (directionX * field.CenterX) + (directionY * field.CenterY) + (directionZ * field.CenterZ);
            if (cosine <= field.EdgeCos)
            {
                continue;
            }

            var covered = cosine >= field.FullCoverCos
                ? 1.0
                : (cosine - field.EdgeCos) / (field.FullCoverCos - field.EdgeCos);

            // Overlapping globes: the one that hides the star most is the one that decides.
            factor = Math.Min(factor, 1.0 - (field.Coverage * covered));
            if (factor <= 0.0)
            {
                return 0.0;
            }
        }

        return Math.Clamp(factor, 0.0, 1.0);
    }

    /// <summary>
    /// Identifies the discs in play, so a star batch built against them can tell when it has gone
    /// stale.
    /// </summary>
    /// <remarks>
    /// Bucketed, and deliberately coarsely: a globe several degrees wide does not change which
    /// stars it hides when it moves a tenth of a degree, and the star batch is already rebuilt on
    /// its own far tighter threshold as the sky turns. A signature any finer would rebuild the
    /// whole star field for a moon that had not gone anywhere.
    /// </remarks>
    public static int GetSignature(IReadOnlyList<SkyOccultingDisc> discs)
    {
        ArgumentNullException.ThrowIfNull(discs);

        var signature = new HashCode();
        for (var index = 0; index < discs.Count; index++)
        {
            var disc = discs[index];
            signature.Add((int)Math.Round(disc.Direction.X * 512.0));
            signature.Add((int)Math.Round(disc.Direction.Y * 512.0));
            signature.Add((int)Math.Round(disc.Direction.Z * 512.0));
            signature.Add((int)Math.Round(disc.AngularDiameterDeg * 8.0));
            signature.Add((int)Math.Round(Math.Clamp(disc.Coverage, 0.0, 1.0) * 20.0));
        }

        return signature.ToHashCode();
    }
}

/// <summary>
/// The near bodies currently drawn in front of the star field, left where the star pass can find
/// them.
/// </summary>
/// <remarks>
/// The passes run in the order they are drawn in -- stars first, then the discs over them -- so the
/// star pass cannot ask the near-body pass what it is about to draw. It reads what that pass placed
/// last frame instead. A frame's lag is nothing here: these bodies move a small fraction of an
/// arcminute in that time, and the star batch they are compared against is itself held to a tenth
/// of a degree.
/// </remarks>
public static class SkyDiscsInFront
{
    /// <summary>The near-body globes placed by the last near-body frame.</summary>
    public static IReadOnlyList<SkyOccultingDisc> NearBodies { get; private set; } = [];

    /// <summary>Hands over the globes the near-body pass has just drawn.</summary>
    public static void PublishNearBodies(IReadOnlyList<SkyOccultingDisc> discs)
        => NearBodies = discs ?? [];

    /// <summary>Forgets everything, for a client shutting down or a pass standing itself down.</summary>
    public static void Clear() => NearBodies = [];
}
