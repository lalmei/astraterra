using AstraTerra.Astronomy;

namespace AstraTerra.Client.Rendering;

/// <summary>One plate's hold over the catalog stars behind it: where it sits, how far it reaches, and how completely it covers.</summary>
public readonly record struct DeepSkyPlateField(
    double CenterX,
    double CenterY,
    double CenterZ,
    double FullCoverCos,
    double EdgeCos,
    double Coverage);

/// <summary>
/// How a deep-sky plate is drawn, and what it does to the catalog stars underneath it.
/// </summary>
/// <remarks>
/// A plate is a photograph, and it arrives with its own stars already in it, spanning 0.002 to 0.03
/// deg. Drawing the catalog's sprites on top of that is drawing the same stars twice at two
/// different scales, and it is the reason a scoped star used to be shrunk hard enough to look wrong
/// everywhere else in the sky.
/// <para>
/// Fading the catalog where a plate covers it puts that constraint where it belongs. Over the
/// Pleiades the photograph's stars are the stars; a degree away, off the plate, the sky is the
/// catalog's again at full size.
/// </para>
/// </remarks>
public static class DeepSkyPlateVisibility
{
    /// <summary>How much of a plate's authored brightness reaches the screen.</summary>
    public const float BrightnessScale = 0.42f;

    /// <summary>Ceiling on a plate's drawn alpha, so a photograph never becomes an opaque wall.</summary>
    public const float MaximumOpacity = 0.7f;

    /// <summary>
    /// The share of a plate's radius covered completely, beyond which its hold ramps off to nothing
    /// at the edge. A photograph's own field thins out towards its border, and a hard edge would
    /// draw a visible disc of missing stars.
    /// </summary>
    public const double FullCoverShare = 0.55;

    /// <summary>Opacity a plate draws at, from the brightness it was projected with.</summary>
    public static float CalculateOpacity(double brightness)
        => (float)Math.Clamp(brightness * BrightnessScale, 0.0, MaximumOpacity);

    /// <summary>
    /// Reduces the plates to what the star loop needs, once per rebuild rather than once per star:
    /// a centre to measure from and the two angles its hold ramps between.
    /// </summary>
    public static void BuildFields(
        IReadOnlyList<RenderedDeepSkyObject> plates,
        List<DeepSkyPlateField> destination)
    {
        ArgumentNullException.ThrowIfNull(plates);
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        for (var index = 0; index < plates.Count; index++)
        {
            var plate = plates[index];
            if (plate.QuadCorners.Count == 0 || plate.AngularSizeDeg <= 0)
            {
                continue;
            }

            // How strongly the plate is being drawn at all, which is its own brightness: a plate
            // still climbing out of the horizon haze cannot take the stars with it. Deliberately not
            // its drawn alpha, which tops out at well under one and would leave every star half-lit
            // on top of the photograph.
            var coverage = Math.Clamp(plate.Brightness, 0.0, 1.0);
            if (coverage <= 0.001)
            {
                continue;
            }

            var centerX = 0.0;
            var centerY = 0.0;
            var centerZ = 0.0;
            for (var corner = 0; corner < plate.QuadCorners.Count; corner++)
            {
                centerX += plate.QuadCorners[corner].X;
                centerY += plate.QuadCorners[corner].Y;
                centerZ += plate.QuadCorners[corner].Z;
            }

            var length = Math.Sqrt((centerX * centerX) + (centerY * centerY) + (centerZ * centerZ));
            if (length < 1e-9)
            {
                continue;
            }

            var edgeRad = plate.AngularSizeDeg * Math.PI / 360.0;

            // Cosines rather than angles, so the star loop compares a dot product and never calls
            // an inverse trigonometric function. A wider angle is a smaller cosine.
            destination.Add(new DeepSkyPlateField(
                centerX / length,
                centerY / length,
                centerZ / length,
                Math.Cos(edgeRad * FullCoverShare),
                Math.Cos(edgeRad),
                coverage));
        }
    }

    /// <summary>
    /// How much of its brightness a star keeps where it lies, which is all of it unless a plate is
    /// drawn over that part of the sky.
    /// </summary>
    public static double GetStarBrightnessFactor(
        IReadOnlyList<DeepSkyPlateField> fields,
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

            // Overlapping plates: the one that hides the star most is the one that decides.
            factor = Math.Min(factor, 1.0 - (field.Coverage * covered));
        }

        return Math.Clamp(factor, 0.0, 1.0);
    }

    /// <summary>
    /// Identifies the set of plates in play, so a star batch built against them can tell when it has
    /// gone stale. Brightness is bucketed because it drifts continuously as a plate climbs.
    /// </summary>
    public static int GetSignature(IReadOnlyList<RenderedDeepSkyObject> plates)
    {
        ArgumentNullException.ThrowIfNull(plates);

        var signature = new HashCode();
        for (var index = 0; index < plates.Count; index++)
        {
            signature.Add(plates[index].Id, StringComparer.Ordinal);
            signature.Add((int)Math.Round(plates[index].Brightness * 20.0));
        }

        return signature.ToHashCode();
    }
}
