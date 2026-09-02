namespace AstraTerra.Astronomy;

/// <summary>
/// One of the moon's eight authored phase assets.
/// </summary>
/// <param name="LitOnRight">
/// True for a waxing face, whose authored bright limb is on the right of the picture. Runtime moon
/// rendering uses the full face and lights it continuously, but the phase set remains a checked
/// asset contract and is useful anywhere a discrete calendar face is needed.
/// </param>
public sealed record MoonPhaseFace(string Id, string DisplayName, bool LitOnRight, string TexturePath);

/// <summary>
/// Where the moon is drawn, how wide, and which way its surface is held.
/// </summary>
/// <remarks>
/// <para>
/// Vintage Story already decides where the moon is and what phase it is in — the calendar reports
/// both, moonlight follows them, and nothing here changes any of that. What this replaces is the
/// picture: a portrait of the real moon in place of the game's own disc, at the same apparent width
/// as the disc it replaces. The renderer lights that fixed portrait rather than rotating a
/// pre-phased picture, so Tycho and the rest of the surface do not travel around the disc with the
/// terminator.
/// </para>
/// <para>
/// Nothing in here draws or touches the game. It is the arithmetic between the calendar's moon and a
/// square of sky, kept apart from the renderer so it can be checked without one.
/// </para>
/// </remarks>
public static class MoonDiscModel
{
    /// <summary>
    /// How wide the moon is drawn: the roughly seven-degree apparent diameter of Vintage Story's
    /// own moon, which is what this picture replaces and what the telescope then magnifies.
    /// </summary>
    public const double AngularDiameterDeg = 7.0;

    /// <summary>
    /// Below this altitude the moon is not drawn. Its own radius is allowed on top of the horizon,
    /// so a moon halfway up is still drawn while its centre is under.
    /// </summary>
    public const double HorizonCutoffDeg = -1.0;

    /// <summary>
    /// The eight faces, in the order Vintage Story's own moon phase runs: new, waxing to full, then
    /// waning back. The ids are the calendar's <c>EnumMoonPhase</c> names in all but spelling —
    /// <c>Empty</c>, <c>Grow1..3</c>, <c>Full</c>, <c>Shrink1..3</c> — and the order is what maps one
    /// to the other.
    /// </summary>
    public static IReadOnlyList<MoonPhaseFace> Faces { get; } =
    [
        new("new", "New Moon", LitOnRight: true, "astraterra:environment/solar-system/moon-new"),
        new("waxing-crescent", "Waxing Crescent", true, "astraterra:environment/solar-system/moon-waxing-crescent"),
        new("first-quarter", "First Quarter", true, "astraterra:environment/solar-system/moon-first-quarter"),
        new("waxing-gibbous", "Waxing Gibbous", true, "astraterra:environment/solar-system/moon-waxing-gibbous"),
        new("full", "Full Moon", true, "astraterra:environment/solar-system/moon-full"),
        new("waning-gibbous", "Waning Gibbous", LitOnRight: false, "astraterra:environment/solar-system/moon-waning-gibbous"),
        new("last-quarter", "Last Quarter", false, "astraterra:environment/solar-system/moon-last-quarter"),
        new("waning-crescent", "Waning Crescent", false, "astraterra:environment/solar-system/moon-waning-crescent")
    ];

    /// <summary>The unshadowed surface portrait used while the renderer supplies the phase.</summary>
    public static MoonPhaseFace FullFace => Faces[4];

    /// <summary>
    /// The face nearest the phase the calendar reports, on a month that wraps: a phase just short of
    /// eight is a new moon again, not a waning crescent.
    /// </summary>
    /// <param name="moonPhaseExact">
    /// The calendar's own continuous phase, 0 at new and running to 8 at the next new moon.
    /// </param>
    public static MoonPhaseFace SelectFace(double moonPhaseExact)
    {
        if (!double.IsFinite(moonPhaseExact))
        {
            return Faces[4];
        }

        var index = (int)Math.Round(moonPhaseExact) % Faces.Count;
        return Faces[index < 0 ? index + Faces.Count : index];
    }

    /// <summary>
    /// Which way the surface portrait's horizontal runs.
    /// </summary>
    /// <remarks>
    /// The portrait hangs level on the local sky. Illumination has its own sunward axis in the moon
    /// mesh, so changing phase turns the shadow without turning Tycho or any other landmark.
    /// </remarks>
    public static SkyDirection SurfaceRightAxis(SkyDirection moon) => SkyTangent.Horizontal(moon);

    /// <summary>The moon's picture as four corners on the sky sphere.</summary>
    public static IReadOnlyList<DeepSkyDirection> BuildQuad(SkyDirection moon)
        => SkyTangent.BuildQuad(moon, SurfaceRightAxis(moon), AngularDiameterDeg);
}
