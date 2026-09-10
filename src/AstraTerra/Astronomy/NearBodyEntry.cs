namespace AstraTerra.Astronomy;

/// <summary>What a near body is, which is what decides how it reads in the sky.</summary>
public enum NearBodyKind
{
    /// <summary>The planet the observer's own world orbits: a moon world's parent giant.</summary>
    ParentPlanet = 0,

    /// <summary>A sibling moon of that planet, or a moon of the observer's own world.</summary>
    Moon = 1
}

/// <summary>
/// The picture to draw for a near body: a square RGBA image whose disc is centred, and how much of
/// that square the body's own disc fills.
/// </summary>
/// <remarks>
/// The image is supplied by whoever authors the body rather than modelled here, because a ringed
/// giant's face is a picture, not a parameter list. Everything outside the disc must be transparent;
/// rings live in that margin, which is what <paramref name="DiscFraction"/> is for -- the shading
/// pass needs to know where the globe ends and the ring plane begins.
/// </remarks>
/// <param name="Size">Width and height of the square image, in pixels.</param>
/// <param name="RgbaPixels">
/// <c>Size * Size</c> pixels in the packed order <see cref="Vintagestory.API.MathTools.ColorUtil"/>
/// uses, ready to upload as a texture.
/// </param>
/// <param name="DiscFraction">
/// The body's own disc as a fraction of the image's half-width: 1 when the image is all globe, less
/// when rings need room around it.
/// </param>
public sealed record NearBodyFace(int Size, int[] RgbaPixels, double DiscFraction);

/// <summary>
/// A near body that goes round the same body the observer's world goes round: a sibling moon
/// circling the parent planet, seen from a world circling it too.
/// </summary>
/// <remarks>
/// <para>
/// An hour angle and a rate can only ever send a body right round the sky, and only the outer
/// siblings do that. A sibling closer in than the observer is bound to the parent the way Venus is
/// bound to the sun: its direction swings back and forth about the parent and never leaves it, out
/// to an elongation of <c>asin(DistanceRatio)</c> and no further. That is not a rate, so it is
/// described here as the orbit it comes from and worked out per frame.
/// </para>
/// <para>
/// Both orbits are taken as circular and coplanar, which is what regular satellites of a giant are
/// to well inside anything the eye catches at this distance.
/// </para>
/// </remarks>
/// <param name="AnchorHourAngleDeg">
/// Hour angle of the body being circled, which for a locked world is a constant: the parent planet
/// hangs at one spot and everything else is placed relative to it.
/// </param>
/// <param name="DistanceRatio">
/// The sibling's orbit over the observer's, both about the parent. Below one for an inner sibling,
/// above one for an outer one.
/// </param>
/// <param name="PhaseDeg">
/// How far ahead of the observer the sibling sits on its orbit at day zero. Zero puts it on the
/// line between the observer and the parent -- transiting the parent when it is inside the
/// observer's orbit, and opposite the parent in the sky when it is outside.
/// </param>
/// <param name="PhaseRateDegPerDay">
/// How fast that lead changes, in world days: <c>360 * (observerPeriod / siblingPeriod - 1)</c>.
/// Negative for an outer sibling, which falls behind.
/// </param>
public sealed record NearBodyOrbit(
    double AnchorHourAngleDeg,
    double DistanceRatio,
    double PhaseDeg,
    double PhaseRateDegPerDay
);

/// <summary>
/// The circle a moon of the observer's own world is actually on, for a sky where the track it keeps
/// matters more than the rate it drifts at.
/// </summary>
/// <remarks>
/// <para>
/// A flat hour-angle rate runs a moon round the same line every night. A real moon does not: its
/// orbit is tilted to the equator the observer measures from, so it climbs to one side of that line
/// and back over a month, and where it rises walks along the horizon as it goes -- Earth's moon takes
/// a month to cross that band. That is a circle on the sky, not a rate, so it is described here as
/// the orbit it comes from and worked out per frame -- the same reason <see cref="NearBodyOrbit"/>
/// exists for a sibling penned beside its parent.
/// </para>
/// <para>
/// The orbit is taken as circular, which regular moons are to well inside anything the eye catches,
/// and it is held in the observer's own equatorial frame rather than fixed to the ground. A body on
/// this track therefore keeps station with the star field the way a moon does, and two observers a
/// world apart see it at the hour angles their own meridians give it, with no prime-meridian
/// convention needed.
/// </para>
/// </remarks>
/// <param name="InclinationDeg">
/// How far the orbit tilts out of the observer's celestial equator, which is how far the body gets
/// from that equator at the top and bottom of its track. Zero is a coplanar moon that runs along the
/// equator itself.
/// </param>
/// <param name="NodeRightAscensionDeg">
/// Right ascension of the ascending node at day zero: where the body crosses the equator going north,
/// and so which part of the sky its track leans into.
/// </param>
/// <param name="ArgumentOfLatitudeDeg">
/// How far round its orbit past that node the body sits at day zero. Zero puts it on the equator and
/// climbing; 90 puts it at the top of its track.
/// </param>
/// <param name="ArgumentRateDegPerDay">
/// How fast it goes round, in world days: <c>360 / month</c>. The hour-angle drift a ground observer
/// sees falls out of this against the turning sky and is not authored separately.
/// </param>
/// <param name="NodeRegressionDegPerDay">
/// How fast the node itself walks around the equator, sliding the whole track around the sky: which
/// stars the body moves against, and which part of the horizon it rises from at the top of its climb.
/// Negative for a regressing node, as Earth's moon has. It does not change how far the track reaches
/// -- that is the inclination's business, and it is held fixed here. Zero holds the track put.
/// </param>
public sealed record NearBodyTrack(
    double InclinationDeg,
    double NodeRightAscensionDeg,
    double ArgumentOfLatitudeDeg,
    double ArgumentRateDegPerDay,
    double NodeRegressionDegPerDay = 0.0
);

/// <summary>
/// A body near enough to show a disc rather than a point: the planet a moon world hangs beneath, or
/// a sibling moon crossing that world's sky.
/// </summary>
/// <remarks>
/// <para>
/// Near bodies are placed by hour angle rather than by right ascension, because the bodies this is
/// for do not keep station with the stars. A tidally locked world's parent planet hangs at one spot
/// on the sky forever -- constant hour angle, zero rate -- while the star field turns behind it. A
/// sibling moon drifts around from there at the rate the two orbits beat against each other.
/// </para>
/// <para>
/// Declination is the body's angle out of the observer's celestial equator, which for a locked world
/// is also its orbital plane: zero puts a body on the same circle the sun travels, which is where
/// coplanar moons belong. It is one fixed angle unless the body carries a <see cref="NearBodyTrack"/>,
/// which is what a moon tilted out of that plane needs instead.
/// </para>
/// </remarks>
/// <param name="AngularDiameterDeg">
/// How wide the whole image draws, rings included -- not the globe alone. A parent giant seen from a
/// close moon is tens of degrees across, which is the point of drawing it at all. For a body with an
/// <paramref name="Orbit"/> this is the width at the parent's own distance, and the drawn width
/// follows the distance from there: a sibling passing close swells, and one round the far side of
/// the parent shrinks.
/// </param>
/// <param name="HourAngleDeg">
/// Hour angle at day zero, measured west from the meridian the way
/// <see cref="CelestialMath.GetHorizontalCoordinates"/> measures it.
/// </param>
/// <param name="HourAngleRateDegPerDay">
/// How fast that hour angle drifts. Zero holds the body still over the ground, which is what tidal
/// locking looks like from the surface.
/// </param>
/// <param name="Orbit">
/// Set when the body circles the parent rather than drifting at a flat rate. It supersedes
/// <paramref name="HourAngleDeg"/> and <paramref name="HourAngleRateDegPerDay"/>, which then only
/// record where the body starts and how fast it comes round on average.
/// </param>
/// <param name="Brightness">
/// How bright the lit face draws, 0 to 1. A dark rocky moon sits well below a fresh-ice one.
/// </param>
/// <param name="Track">
/// Set when the body runs a tilted circle about the observer's own world rather than drifting along
/// one line of declination. It supersedes <paramref name="DeclinationDeg"/> entirely, and supersedes
/// <paramref name="HourAngleDeg"/> and <paramref name="HourAngleRateDegPerDay"/>, which then only
/// record where the body started and how fast it comes round on average. A body carries a
/// <paramref name="Track"/> or an <paramref name="Orbit"/>, never both: one goes round the observer,
/// the other goes round the parent they both orbit.
/// </param>
public sealed record NearBodyEntry(
    string Id,
    string DisplayName,
    NearBodyKind Kind,
    double AngularDiameterDeg,
    double HourAngleDeg,
    double HourAngleRateDegPerDay,
    double DeclinationDeg,
    double Brightness,
    NearBodyFace Face,
    NearBodyOrbit? Orbit = null,
    NearBodyTrack? Track = null
);

/// <summary>
/// The near bodies of the observer's own world, and whether Vintage Story's moon still belongs in
/// that sky.
/// </summary>
/// <param name="HidesVanillaMoon">
/// True when these bodies replace the game's moon rather than joining it. A world that is itself a
/// moon has no moon of its own, so leaving the vanilla one up would put a second, unrelated body
/// beside the parent planet.
/// </param>
public sealed record NearBodyCatalog(
    int SchemaVersion,
    bool HidesVanillaMoon,
    IReadOnlyList<NearBodyEntry> Bodies
)
{
    public const int CurrentSchemaVersion = 1;

    public static NearBodyCatalog Empty { get; } = new(CurrentSchemaVersion, false, []);
}
