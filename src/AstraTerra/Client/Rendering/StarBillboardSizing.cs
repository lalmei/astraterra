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
    /// How much of its sprite's quad the drawn star actually fills, measured off the shipped
    /// textures as the diameter within which the ink falls to half its peak, as a share of the quad.
    /// </summary>
    /// <remarks>
    /// The scoped view swaps sprites — rays for an Airy disc, because the rays are scintillation and
    /// a telescope takes the air out — and the two do not fill their quads alike. A bright star's
    /// core covers 0.87 of the naked-eye sprite and 0.43 of the scoped one, so at an unchanged
    /// billboard size raising the scope halved every bright star on screen. Faint stars barely moved,
    /// 0.48 to 0.43, which is why it read as the bright stars shrinking rather than as the sky
    /// receding.
    /// <para>
    /// Re-measure these if the sprites are redrawn; nothing else here knows what the art looks like.
    /// </para>
    /// </remarks>
    public const float NakedEyeBrightCoreCoverage = 0.87f;
    public const float NakedEyeFaintCoreCoverage = 0.48f;
    public const float ScopedCoreCoverage = 0.43f;

    /// <summary>
    /// How much of its naked-eye angular size a star keeps at this magnification, before the sprite
    /// swap is accounted for.
    /// </summary>
    /// <remarks>
    /// Sky billboards are sized in degrees, so magnification enlarges them: left alone, the 0.48 deg
    /// that makes a bright star eight pixels wide at the naked eye would cover sixty-seven at the
    /// brass telescope's field and over a hundred at the precision telescope's. A star is
    /// unresolvable at any magnification — it gets brighter, not bigger — so scaling by the
    /// field-of-view multiplier holds it at a constant handful of screen pixels however far the scope
    /// is wound in, which is what an eyepiece shows.
    /// <para>
    /// This used to be scaled down further still, to keep a sprite from dwarfing the 0.002 to 0.03
    /// deg stars photographed into the deep-sky plates it is drawn over. That is now
    /// <see cref="DeepSkyPlateVisibility"/>'s job, and it does it only where a plate actually is,
    /// rather than shrinking every star in the sky for the sake of the few that land on one.
    /// </para>
    /// </remarks>
    public static float CalculateStarAngularScale(bool scoped, float fovMultiplier)
        => scoped ? Math.Clamp(fovMultiplier, 0.0f, 1.0f) : 1.0f;

    /// <summary>
    /// What a bright star's billboard scales by when scoped: the field, widened so the swapped sprite
    /// draws the same star the naked eye was showing rather than one half its size.
    /// </summary>
    public static float CalculateBrightStarAngularScale(bool scoped, float fovMultiplier)
        => CalculateStarAngularScale(scoped, fovMultiplier)
            * (scoped ? NakedEyeBrightCoreCoverage / ScopedCoreCoverage : 1.0f);

    /// <summary>The same for a faint star, whose two sprites are much closer in how much they fill.</summary>
    public static float CalculateFaintStarAngularScale(bool scoped, float fovMultiplier)
        => CalculateStarAngularScale(scoped, fovMultiplier)
            * (scoped ? NakedEyeFaintCoreCoverage / ScopedCoreCoverage : 1.0f);

    /// <summary>
    /// How much of its naked-eye angular size a planet keeps at this magnification.
    /// </summary>
    /// <remarks>
    /// Held at a floor rather than tracking the field of view, because a planet is the one thing near
    /// enough for a telescope to resolve. Wide open it is barely larger than the stars beside it;
    /// wound all the way in it is several times their size and reads as a disc.
    /// </remarks>
    public static float CalculatePlanetAngularScale(bool scoped, float fovMultiplier)
        => scoped
            ? Math.Max(Math.Clamp(fovMultiplier, 0.0f, 1.0f), ScopedPlanetAngularFloor)
                * (NakedEyeBrightCoreCoverage / ScopedCoreCoverage)
            : 1.0f;
}
