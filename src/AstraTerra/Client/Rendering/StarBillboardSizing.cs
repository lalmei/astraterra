namespace AstraTerra.Client.Rendering;

public static class StarBillboardSizing
{
    public const float MinimumCoreDiameterPixels = 1.75f;
    public const float ProjectedSizeScale = 0.25f;
    public const float MaximumCoreDiameterPixels = 6.0f;
    public const float GlowDiameterRange = 1.0f;

    public static float CalculateCoreDiameterPixels(double projectedSize)
        => (float)Math.Clamp(
            projectedSize * ProjectedSizeScale,
            MinimumCoreDiameterPixels,
            MaximumCoreDiameterPixels);

    public static float CalculateGlowDiameterPixels(float coreDiameterPixels, float normalizedBrightness)
        => coreDiameterPixels * (1.0f + (GlowDiameterRange * Math.Clamp(normalizedBrightness, 0.0f, 1.0f)));
}
