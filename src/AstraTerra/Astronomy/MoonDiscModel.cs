namespace AstraTerra.Astronomy;

/// <summary>
/// One of the moon's eight faces: the picture to draw, and which limb of it is lit.
/// </summary>
/// <param name="LitOnRight">
/// True for a waxing face, whose bright limb is on the right of the picture. It is what turns the
/// image the right way round in the sky: the bright limb has to point at the sun, and which edge of
/// the picture that is depends on where in the month the moon is.
/// </param>
public sealed record MoonPhaseFace(string Id, string DisplayName, bool LitOnRight, string TexturePath);

/// <summary>
/// Where the moon is drawn, how wide, which face, and which way up.
/// </summary>
/// <remarks>
/// <para>
/// Vintage Story already decides where the moon is and what phase it is in — the calendar reports
/// both, moonlight follows them, and nothing here changes any of that. What this replaces is the
/// picture: eight photographs of the real moon in place of the game's own disc, on a disc sized by
/// what a telescope raised on it should show.
/// </para>
/// <para>
/// Nothing in here draws or touches the game. It is the arithmetic between the calendar's moon and a
/// square of sky, kept apart from the renderer so it can be checked without one.
/// </para>
/// </remarks>
public static class MoonDiscModel
{
    /// <summary>The angle the moon really subtends: half a degree, and famously smaller than anyone remembers.</summary>
    public const double TrueAngularDiameterDeg = 0.52;

    /// <summary>
    /// How much larger than life the moon is drawn.
    /// </summary>
    /// <remarks>
    /// A compromise between two sizes that are both wrong. Vintage Story's own moon is about seven
    /// degrees across — thirteen times life size, which is roughly what everyone pictures a moon as
    /// and is why the true angle reads as a pinprick beside it. But seven degrees overflows the
    /// precision telescope's field entirely, so a scope raised on it would show a crop of the surface
    /// rather than the moon, and the pictures are only a couple of hundred pixels across, which is
    /// already less than a moon that size wants.
    /// <para>
    /// Four times life is what those meet at: substantial to the naked eye, and about half the field
    /// at the highest magnification, which is the view an eyepiece actually gives.
    /// </para>
    /// </remarks>
    public const double SizeExaggeration = 3.85;

    /// <summary>How wide the moon is drawn, which is what the telescope then magnifies.</summary>
    public const double AngularDiameterDeg = TrueAngularDiameterDeg * SizeExaggeration;

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
    /// Which way the picture's horizontal runs, so that its bright limb points at the sun.
    /// </summary>
    /// <remarks>
    /// The one thing about a moon that everybody notices when it is wrong. The bright limb faces the
    /// sun always — that is what a phase is — so the picture is turned until it does, rather than hung
    /// level and left to disagree with the sky. A waxing face is lit on its right, so its right edge
    /// goes sunward; a waning face is lit on its left, so its left edge does.
    /// <para>
    /// A full moon has the sun behind the observer, where it has no direction on the sky to point at,
    /// and the picture is symmetric anyway: it hangs level.
    /// </para>
    /// </remarks>
    public static SkyDirection RightAxis(SkyDirection moon, SkyDirection sun, bool litOnRight)
    {
        var sunward = SkyTangent.Tangent(moon, sun, fallback: SkyTangent.Horizontal(moon));
        return litOnRight ? sunward : SkyTangent.Negate(sunward);
    }

    /// <summary>The moon's picture as four corners on the sky sphere.</summary>
    public static IReadOnlyList<DeepSkyDirection> BuildQuad(SkyDirection moon, SkyDirection sun, MoonPhaseFace face)
    {
        ArgumentNullException.ThrowIfNull(face);
        return SkyTangent.BuildQuad(moon, RightAxis(moon, sun, face.LitOnRight), AngularDiameterDeg);
    }
}
