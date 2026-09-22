using AstraTerra.Astronomy;
using AstraTerra.Client.Observation;

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
    /// <summary>How much of a plate's authored brightness reaches the screen at full zoom reveal.</summary>
    public const float BrightnessScale = 0.42f;

    /// <summary>Ceiling on a plate's drawn alpha, so a photograph never becomes an opaque wall.</summary>
    public const float MaximumOpacity = 0.7f;

    /// <summary>Telescope wide-field FOV multiplier where zoom reveal is at its minimum.</summary>
    public const float WideTelescopeFovMultiplier = 0.45f;

    /// <summary>Brass telescope maximum zoom FOV multiplier where reveal reaches full strength.</summary>
    public const float BrassMaxZoomFovMultiplier = TelescopeObservationState.MaxZoomFovMultiplier;

    /// <summary>Plate visibility at wide field — a faint findable smudge, not a hard rectangle.</summary>
    public const float MinZoomReveal = 0.05f;

    /// <summary>Plate visibility once brass (or tighter) magnification has resolved the photograph.</summary>
    public const float MaxZoomReveal = 1.0f;

    /// <summary>
    /// Natural darkness below which plates are gone entirely, and above which they fade in. Higher
    /// than <see cref="MilkyWayVisibility.TwilightFloor"/> because a telescopic photograph is surface
    /// brightness, not a point source — it must not survive twilight the way catalog stars can.
    /// </summary>
    public const double TwilightFloor = 0.65;

    /// <summary>
    /// The share of a plate's radius covered completely, beyond which its hold ramps off to nothing
    /// at the edge. A photograph's own field thins out towards its border, and a hard edge would
    /// draw a visible disc of missing stars.
    /// </summary>
    public const double FullCoverShare = 0.55;

    /// <summary>
    /// How much ambient night remains for extended deep-sky plates, from natural darkness alone.
    /// Deliberately not the star pass's render darkness: forced daylight stars must not reveal
    /// photographs the sky glow would wash out anyway.
    /// </summary>
    public static float CalculateAmbientVisibility(double naturalDarkness)
    {
        if (!double.IsFinite(naturalDarkness))
        {
            return 0f;
        }

        var night = Math.Clamp((naturalDarkness - TwilightFloor) / (1.0 - TwilightFloor), 0.0, 1.0);
        return (float)night;
    }

    /// <summary>Zoom reveal and ambient night combined — shared by drawn alpha and star handover.</summary>
    public static float CalculateVisibilityFactor(float fovMultiplier, double naturalDarkness)
    {
        return CalculateZoomReveal(fovMultiplier) * CalculateAmbientVisibility(naturalDarkness);
    }

    /// <summary>
    /// How much of a plate's authored detail is visible at the current telescope field of view.
    /// Ease-in quadratic from wide field to brass maximum zoom; at or below brass max, reveal is full.
    /// </summary>
    public static float CalculateZoomReveal(float fovMultiplier)
    {
        if (!float.IsFinite(fovMultiplier) || fovMultiplier <= 0f)
        {
            return MinZoomReveal;
        }

        if (fovMultiplier >= WideTelescopeFovMultiplier)
        {
            return MinZoomReveal;
        }

        if (fovMultiplier <= BrassMaxZoomFovMultiplier)
        {
            return MaxZoomReveal;
        }

        var span = WideTelescopeFovMultiplier - BrassMaxZoomFovMultiplier;
        var progress = (WideTelescopeFovMultiplier - fovMultiplier) / span;
        var eased = progress * progress;
        return MinZoomReveal + ((MaxZoomReveal - MinZoomReveal) * eased);
    }

    /// <summary>Opacity a plate draws at, from brightness, telescope zoom reveal, and ambient night.</summary>
    public static float CalculateOpacity(double brightness, float fovMultiplier, double naturalDarkness)
    {
        var visibility = CalculateVisibilityFactor(fovMultiplier, naturalDarkness);
        return (float)Math.Clamp(brightness * BrightnessScale * visibility, 0.0, MaximumOpacity);
    }

    /// <summary>
    /// Reduces the plates to what the star loop needs, once per rebuild rather than once per star:
    /// a centre to measure from and the two angles its hold ramps between.
    /// </summary>
    public static void BuildFields(
        IReadOnlyList<RenderedDeepSkyObject> plates,
        float fovMultiplier,
        double naturalDarkness,
        List<DeepSkyPlateField> destination)
    {
        ArgumentNullException.ThrowIfNull(plates);
        ArgumentNullException.ThrowIfNull(destination);

        var visibility = CalculateVisibilityFactor(fovMultiplier, naturalDarkness);
        destination.Clear();
        for (var index = 0; index < plates.Count; index++)
        {
            var plate = plates[index];
            if (plate.QuadCorners.Count == 0 || plate.AngularSizeDeg <= 0)
            {
                continue;
            }

            // How strongly the plate is being drawn at all, scaled by zoom reveal and ambient night:
            // at wide field or in daylight a plate must not punch a hole in the catalog before the
            // photograph itself is visible.
            var coverage = Math.Clamp(plate.Brightness * visibility, 0.0, 1.0);
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
    /// Identifies the set of plates in play and the telescope zoom reveal applied to them, so a star
    /// batch built against them can tell when it has gone stale. Brightness is bucketed because it
    /// drifts continuously as a plate climbs.
    /// </summary>
    public static int GetSignature(
        IReadOnlyList<RenderedDeepSkyObject> plates,
        float fovMultiplier,
        double naturalDarkness)
    {
        ArgumentNullException.ThrowIfNull(plates);

        var signature = new HashCode();
        signature.Add((int)Math.Round(CalculateVisibilityFactor(fovMultiplier, naturalDarkness) * 100.0));
        for (var index = 0; index < plates.Count; index++)
        {
            signature.Add(plates[index].Id, StringComparer.Ordinal);
            signature.Add((int)Math.Round(plates[index].Brightness * 20.0));
        }

        return signature.ToHashCode();
    }
}
