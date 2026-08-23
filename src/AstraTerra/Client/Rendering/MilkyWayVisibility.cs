namespace AstraTerra.Client.Rendering;

/// <summary>
/// How strongly the Milky Way draws, given the night it is being drawn into.
/// </summary>
/// <remarks>
/// A star is a point source, so it survives a bright sky: it stays visible in twilight and next to a
/// full moon because all of its light arrives in one place. The Milky Way is the opposite — surface
/// brightness spread over a quarter of the sky — and it is the first thing any sky glow takes away.
/// That is why this is not simply the star pass's brightness bias reused: the band has to be gone
/// well before the stars are, or it reads as a painted-on decal that ignores the night around it.
/// </remarks>
public static class MilkyWayVisibility
{
    /// <summary>Alpha the band draws at on a moonless night at full darkness.</summary>
    public const float MaximumOpacity = 0.5f;

    /// <summary>Darkness below which the band is gone entirely, and above which it fades in.</summary>
    public const double TwilightFloor = 0.55;

    /// <summary>How much of the band a full moon washes out.</summary>
    public const double FullMoonWashout = 0.85;

    /// <summary>
    /// Opacity the band draws at, from the night's darkness, the moon, and the player's setting.
    /// </summary>
    /// <param name="darkness">1 minus the daylight strength, or 1 when daylight stars are forced.</param>
    /// <param name="moonPhaseBrightness">
    /// The moon's brightness for its phase. Deliberately not gated on whether the moon is up: this
    /// is the mod's one existing measure of how bright the night is, and a wrong band on the few
    /// nights a bright moon is below the horizon costs less than a second moon model would.
    /// </param>
    /// <param name="configuredBrightness">The player's multiplier; zero switches the band off.</param>
    public static float CalculateOpacity(
        double darkness,
        double moonPhaseBrightness,
        double configuredBrightness)
    {
        if (configuredBrightness <= 0 || !double.IsFinite(configuredBrightness))
        {
            return 0f;
        }

        var night = Math.Clamp((darkness - TwilightFloor) / (1.0 - TwilightFloor), 0.0, 1.0);
        var moonlit = 1.0 - (FullMoonWashout * Math.Clamp(moonPhaseBrightness, 0.0, 1.0));
        var opacity = MaximumOpacity * night * moonlit * configuredBrightness;

        return (float)Math.Clamp(opacity, 0.0, 1.0);
    }
}
