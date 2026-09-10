using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// A moon of the observer's own world, on the tilted circle it actually goes round rather than along
/// one line of declination. What a player sees of the difference is a moon that rises further north
/// some nights than others, and moves against different stars as the months pass.
/// </summary>
public sealed class NearBodyTrackTests
{
    private const double LatitudeDeg = 20.0;
    private const double MonthDays = 29.53;

    /// <summary>
    /// The model this replaces gave a moon a flat hour-angle drift of <c>360 * (1 - 1 / month)</c> --
    /// the world's own turn less the moon's month, which is why Earth's rises about fifty minutes
    /// later each day. A coplanar track has to reproduce exactly that, or every world already
    /// generated would have its moons rising at new times.
    /// </summary>
    [Fact]
    public void A_Coplanar_Track_Is_The_Flat_Rate_It_Replaces()
    {
        var moon = Moon(Track(inclinationDeg: 0.0));
        var expectedDriftPerDay = 360.0 * (1.0 - (1.0 / MonthDays));

        for (var day = 0.0; day <= 3.0; day += 0.5)
        {
            // The ground has to be allowed to turn: the old flat rate was the world's own turn less
            // the month, with the rotation baked into the body's rate. Here the rotation is the
            // sidereal angle's business and only the moon's own motion is authored, so the drift a
            // standing observer sees is the difference of the two.
            Assert.Equal(0.0, NearBodyRenderModel.DeclinationDeg(moon, day), 9);
            Assert.Equal(
                CelestialMath.NormalizeDegrees(expectedDriftPerDay * day),
                HourAngleFromSky(moon, day, localSiderealDeg: 360.0 * day),
                6);
        }
    }

    /// <summary>
    /// The whole point of the track: the moon climbs to its inclination, comes back through the
    /// equator, goes as far the other way, and is where it started a month later.
    /// </summary>
    [Fact]
    public void A_Tilted_Moon_Climbs_To_Its_Inclination_And_Back_Over_A_Month()
    {
        var moon = Moon(Track(inclinationDeg: 25.0));
        var quarter = MonthDays / 4.0;

        Assert.Equal(0.0, NearBodyRenderModel.DeclinationDeg(moon, 0.0), 9);
        Assert.Equal(25.0, NearBodyRenderModel.DeclinationDeg(moon, quarter), 6);
        Assert.Equal(0.0, NearBodyRenderModel.DeclinationDeg(moon, quarter * 2.0), 6);
        Assert.Equal(-25.0, NearBodyRenderModel.DeclinationDeg(moon, quarter * 3.0), 6);
        Assert.Equal(0.0, NearBodyRenderModel.DeclinationDeg(moon, MonthDays), 6);

        // And it never leaves the band, which is what keeps a moon a moon rather than a comet.
        for (var day = 0.0; day <= MonthDays; day += MonthDays / 200.0)
        {
            Assert.InRange(NearBodyRenderModel.DeclinationDeg(moon, day), -25.0, 25.0);
        }
    }

    /// <summary>
    /// Where it rises moves with that climb. A moon on one fixed declination runs the same track every
    /// night; this one comes up further north at the top of its month than at the bottom of it, which
    /// is the part of the difference a player standing outside can actually see.
    /// </summary>
    [Fact]
    public void Where_It_Rises_Walks_Along_The_Horizon_Through_The_Month()
    {
        var moon = Moon(Track(inclinationDeg: 25.0));
        var sun = new SkyDirection(0.0, -1.0, 0.0);
        var quarter = MonthDays / 4.0;

        var atTheTop = RisingAzimuthDeg(moon, quarter, sun);
        var atTheBottom = RisingAzimuthDeg(moon, quarter * 3.0, sun);

        Assert.True(
            atTheTop < atTheBottom - 20.0,
            $"the moon rose at {atTheTop:0.0} deg at the top of its month and {atTheBottom:0.0} at the bottom");
    }

    /// <summary>
    /// A tilted circle does not project onto the equator evenly: the moon covers equatorial ground
    /// fastest where its track crosses and slowest at the top, and the two only agree at the crossings
    /// and the extremes. Advancing right ascension at a flat rate instead would leave the moon out of
    /// step with its own declination for most of every month.
    /// </summary>
    [Fact]
    public void The_Track_Is_Reduced_To_The_Equator_Rather_Than_Run_Along_It()
    {
        var track = Track(inclinationDeg: 25.0);
        var eighth = MonthDays / 8.0;

        // An eighth of the way round is 45 degrees of orbit, and less than 45 along the equator.
        var offset = NearBodyRenderModel.EquatorialOffsetDeg(track, eighth);
        Assert.True(offset < 45.0, $"the equatorial offset ran ahead of the orbit at {offset:0.000} deg");
        Assert.True(offset > 40.0, $"the equatorial offset fell implausibly far behind at {offset:0.000} deg");

        // The crossings and the extremes are where the two agree exactly, in both directions.
        Assert.Equal(0.0, NearBodyRenderModel.EquatorialOffsetDeg(track, 0.0), 9);
        Assert.Equal(90.0, NearBodyRenderModel.EquatorialOffsetDeg(track, MonthDays / 4.0), 6);
        Assert.Equal(180.0, Math.Abs(NearBodyRenderModel.EquatorialOffsetDeg(track, MonthDays / 2.0)), 6);

        // Over a whole month it still comes round exactly once, so nothing accumulates.
        var wentRound = CelestialMath.NormalizeDegrees(
            NearBodyRenderModel.RightAscensionDeg(Moon(track), MonthDays, 0.0)
            - NearBodyRenderModel.RightAscensionDeg(Moon(track), 0.0, 0.0));
        Assert.True(
            Math.Min(wentRound, 360.0 - wentRound) < 1e-6,
            $"a month of orbit left the moon {wentRound:0.000000} deg from where it started");
    }

    /// <summary>
    /// A tracked moon keeps station with the stars, not with the ground: unlike a locked world's parent
    /// giant it does not hang over one patch, and unlike a body on an authored hour angle it needs no
    /// prime-meridian convention -- two observers a world apart see it at the hour angles their own
    /// meridians give it.
    /// </summary>
    [Fact]
    public void It_Is_Fixed_To_The_Stars_And_Not_To_The_Ground()
    {
        var moon = Moon(Track(inclinationDeg: 25.0));

        // Right ascension is the observer's business not at all: same moment, same place in the sky.
        var here = NearBodyRenderModel.RightAscensionDeg(moon, 4.0, localSiderealDeg: 0.0);
        var aQuarterWorldEast = NearBodyRenderModel.RightAscensionDeg(
            moon,
            4.0,
            localSiderealDeg: 90.0,
            observerLongitudeDeg: 90.0);
        Assert.Equal(here, aQuarterWorldEast, 9);

        // Which means their hour angles differ by exactly the longitude between them.
        Assert.Equal(
            90.0,
            CelestialMath.NormalizeDegrees(
                HourAngleFromSky(moon, 4.0, localSiderealDeg: 90.0) - HourAngleFromSky(moon, 4.0)),
            6);
    }

    /// <summary>
    /// The node's slow walk slides the whole track around the sky without changing how far it reaches:
    /// the moon comes to the top of its climb against different stars a decade later, and no higher.
    /// </summary>
    [Fact]
    public void A_Walking_Node_Slides_The_Track_Without_Changing_Its_Reach()
    {
        var moon = Moon(Track(inclinationDeg: 25.0) with { NodeRegressionDegPerDay = -0.053 });
        var quarter = MonthDays / 4.0;
        var aDecadeOn = (360.0 * 10.0) + quarter;

        Assert.Equal(
            NearBodyRenderModel.DeclinationDeg(moon, quarter),
            NearBodyRenderModel.DeclinationDeg(moon, quarter + (MonthDays * 123.0)),
            6);

        var thenDeg = NearBodyRenderModel.RightAscensionDeg(moon, quarter, 0.0);
        var nowDeg = NearBodyRenderModel.RightAscensionDeg(moon, aDecadeOn, 0.0);
        Assert.True(
            Math.Abs(CelestialMath.NormalizeDegrees(nowDeg - thenDeg)) > 5.0,
            "a decade of node regression left the track in the same place among the stars");
    }

    /// <summary>The azimuth the body comes up at, found by walking the sky round until it clears.</summary>
    private static double RisingAzimuthDeg(NearBodyEntry body, double totalDays, SkyDirection sun)
    {
        for (var sidereal = 0.0; sidereal < 360.0; sidereal += 0.25)
        {
            if (NearBodyRenderModel.Place(body, totalDays, LatitudeDeg, sidereal, sun) is { } placed
                && placed.AltitudeDeg > 0.0)
            {
                var horizontal = CelestialMath.GetHorizontalCoordinates(
                    NearBodyRenderModel.RightAscensionDeg(body, totalDays, sidereal),
                    NearBodyRenderModel.DeclinationDeg(body, totalDays),
                    LatitudeDeg,
                    sidereal);
                return horizontal.AzimuthDeg;
            }
        }

        throw new InvalidOperationException("The body never rose at all.");
    }

    private static double HourAngleFromSky(NearBodyEntry body, double totalDays, double localSiderealDeg = 0.0)
        => CelestialMath.NormalizeDegrees(
            localSiderealDeg - NearBodyRenderModel.RightAscensionDeg(body, totalDays, localSiderealDeg));

    private static NearBodyTrack Track(double inclinationDeg)
        => new(
            inclinationDeg,
            NodeRightAscensionDeg: 0.0,
            ArgumentOfLatitudeDeg: 0.0,
            ArgumentRateDegPerDay: 360.0 / MonthDays);

    private static NearBodyEntry Moon(NearBodyTrack track)
        => new(
            "home-moon",
            "the moon",
            NearBodyKind.Moon,
            AngularDiameterDeg: 0.52,
            HourAngleDeg: 0.0,
            HourAngleRateDegPerDay: 360.0 * (1.0 - (1.0 / MonthDays)),
            DeclinationDeg: 0.0,
            Brightness: 0.5,
            new NearBodyFace(2, new int[4], DiscFraction: 1.0),
            Track: track);
}
