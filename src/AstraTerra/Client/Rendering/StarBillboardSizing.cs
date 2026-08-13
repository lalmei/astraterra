namespace AstraTerra.Client.Rendering;

public static class StarBillboardSizing
{
    public const float MinimumCoreDiameterPixels = 1.75f;
    public const float ProjectedSizeScale = 0.42f;
    public const float MaximumCoreDiameterPixels = 8.0f;
    public const float GlowDiameterRange = 1.0f;

    public static float CalculateCoreDiameterPixels(double projectedSize)
        => (float)Math.Clamp(
            projectedSize * ProjectedSizeScale,
            MinimumCoreDiameterPixels,
            MaximumCoreDiameterPixels);

    public static float CalculateGlowDiameterPixels(float coreDiameterPixels, float normalizedBrightness)
        => coreDiameterPixels * (1.0f + (GlowDiameterRange * Math.Clamp(normalizedBrightness, 0.0f, 1.0f)));

    /// <summary>
    /// Smallest share of its naked-eye angular size a planet keeps when scoped. Because it does not
    /// fall away with magnification the way a star's does, zooming in makes a planet grow on screen —
    /// which is the point of pointing a telescope at one.
    /// </summary>
    public const float ScopedPlanetAngularFloor = 0.5f;

    /// <summary>
    /// How much of its naked-eye angular size a star keeps at this magnification.
    /// </summary>
    /// <remarks>
    /// Sky billboards are sized in degrees, so magnification enlarges them: at the naked eye a bright
    /// star is about eight screen pixels, and left alone the same 0.48 deg would cover sixty-seven at
    /// the brass telescope's field and over a hundred at the precision telescope's. That is wrong
    /// twice over. A star is unresolvable at any magnification — it gets brighter, not bigger — and
    /// the scoped sky is drawn over deep-sky plates whose own stars span 0.002 to 0.03 deg, so an
    /// unscaled sprite sits on the Pleiades at a third the width of the whole photograph.
    /// <para>
    /// Scaling by the field-of-view multiplier holds a star at a constant handful of screen pixels
    /// however far the scope is wound in, which is both what an eyepiece shows and what the plates
    /// need it to be.
    /// </para>
    /// </remarks>
    public static float CalculateStarAngularScale(bool scoped, float fovMultiplier)
        => scoped ? Math.Clamp(fovMultiplier, 0.0f, 1.0f) : 1.0f;

    /// <summary>
    /// How much of its naked-eye angular size a planet keeps at this magnification.
    /// </summary>
    /// <remarks>
    /// Held at a floor rather than tracking the field of view, because a planet is the one thing near
    /// enough for a telescope to resolve. Wide open it is barely larger than the stars beside it;
    /// wound all the way in it is several times their size and reads as a disc.
    /// </remarks>
    public static float CalculatePlanetAngularScale(bool scoped, float fovMultiplier)
        => scoped ? Math.Max(Math.Clamp(fovMultiplier, 0.0f, 1.0f), ScopedPlanetAngularFloor) : 1.0f;
}
