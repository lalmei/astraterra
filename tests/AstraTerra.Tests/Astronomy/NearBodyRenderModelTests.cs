using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class NearBodyRenderModelTests
{
    private const double LatitudeDeg = 20.0;

    /// <summary>
    /// The whole point of the fixed rate: a tidally locked world's parent planet hangs over one
    /// spot on the ground and stays there while the star field turns behind it.
    /// </summary>
    [Fact]
    public void A_Body_With_No_Hour_Angle_Rate_Stays_Put_As_The_Sky_Turns()
    {
        var parent = Parent(hourAngleDeg: 25.0, rate: 0.0);
        var sun = new SkyDirection(0.0, -1.0, 0.0);

        var atNoon = NearBodyRenderModel.Place(parent, 10.0, LatitudeDeg, 0.0, sun);
        var sixHoursLater = NearBodyRenderModel.Place(parent, 10.25, LatitudeDeg, 90.0, sun);
        var aYearOn = NearBodyRenderModel.Place(parent, 375.0, LatitudeDeg, 217.5, sun);

        Assert.NotNull(atNoon);
        Assert.NotNull(sixHoursLater);
        Assert.NotNull(aYearOn);
        Assert.Equal(atNoon.AltitudeDeg, sixHoursLater.AltitudeDeg, 9);
        Assert.Equal(atNoon.AltitudeDeg, aYearOn.AltitudeDeg, 9);
        Assert.Equal(atNoon.Direction.X, aYearOn.Direction.X, 9);
        Assert.Equal(atNoon.Direction.Y, aYearOn.Direction.Y, 9);
        Assert.Equal(atNoon.Direction.Z, aYearOn.Direction.Z, 9);

        // It is only standing still over the ground: among the stars it has moved a whole turn.
        Assert.Equal(335.0, NearBodyRenderModel.RightAscensionDeg(parent, 10.0, 0.0), 6);
        Assert.Equal(65.0, NearBodyRenderModel.RightAscensionDeg(parent, 10.25, 90.0), 6);
    }

    [Fact]
    public void A_Sibling_Moon_Drifts_Around_At_Its_Own_Rate()
    {
        var sibling = Parent(hourAngleDeg: 0.0, rate: 120.0) with { Id = "sibling", Kind = NearBodyKind.Moon };

        Assert.Equal(0.0, NearBodyRenderModel.RightAscensionDeg(sibling, 0.0, 0.0), 6);
        Assert.Equal(240.0, NearBodyRenderModel.RightAscensionDeg(sibling, 1.0, 0.0), 6);
        Assert.Equal(0.0, NearBodyRenderModel.RightAscensionDeg(sibling, 3.0, 0.0), 6);
    }

    [Fact]
    public void A_Body_Below_The_Horizon_Is_Not_Placed()
    {
        var belowFoot = Parent(hourAngleDeg: 180.0, rate: 0.0) with { DeclinationDeg = -80.0 };

        Assert.Null(NearBodyRenderModel.Place(belowFoot, 0.0, 60.0, 0.0, new SkyDirection(0, 1, 0)));
    }

    [Fact]
    public void A_Body_Opposite_The_Sun_Is_Full_And_One_In_Front_Of_It_Is_New()
    {
        var toward = new SkyDirection(1.0, 0.0, 0.0);
        var away = new SkyDirection(-1.0, 0.0, 0.0);
        var sideways = new SkyDirection(0.0, 0.0, 1.0);

        Assert.Equal(0.0, NearBodyRenderModel.IlluminatedFraction(toward, toward), 9);
        Assert.Equal(1.0, NearBodyRenderModel.IlluminatedFraction(away, toward), 9);
        Assert.Equal(0.5, NearBodyRenderModel.IlluminatedFraction(sideways, toward), 9);
    }

    /// <summary>
    /// The terminator is what makes a disc read as a globe, so it has to fall where the sun puts it:
    /// across the middle at quarter phase, and nowhere at all when the disc is fully lit.
    /// </summary>
    [Fact]
    public void The_Terminator_Follows_The_Sun_Across_The_Face()
    {
        // Sun square to the right of the body: the right limb is lit, the left is night.
        var litSide = NearBodyMeshBuilder.LightAt(0.6, 0.0, sunRight: 1.0, sunUp: 0.0, sunToward: 0.0, 0.5);
        var darkSide = NearBodyMeshBuilder.LightAt(-0.6, 0.0, sunRight: 1.0, sunUp: 0.0, sunToward: 0.0, 0.5);
        var onTheLine = NearBodyMeshBuilder.LightAt(0.0, 0.0, sunRight: 1.0, sunUp: 0.0, sunToward: 0.0, 0.5);

        Assert.True(litSide > 0.6f, $"the lit limb came out at {litSide}");
        Assert.True(darkSide <= NearBodyMeshBuilder.NightSideLight + 0.001f, $"the night side came out at {darkSide}");
        Assert.InRange(onTheLine, 0.3f, 0.75f);

        // Sun behind the observer: the whole face is lit, and brightest in the middle.
        var middle = NearBodyMeshBuilder.LightAt(0.0, 0.0, 0.0, 0.0, sunToward: 1.0, 1.0);
        var limb = NearBodyMeshBuilder.LightAt(0.95, 0.0, 0.0, 0.0, sunToward: 1.0, 1.0);
        Assert.True(middle > 0.95f, $"a full face came out at {middle}");
        Assert.True(limb < middle, "the limb should darken toward the edge");
        Assert.True(limb > NearBodyMeshBuilder.NightSideLight);
    }

    [Fact]
    public void The_Ring_Plane_Dims_With_The_Planets_Phase()
    {
        var atFull = NearBodyMeshBuilder.LightAt(1.6, 0.0, 0.0, 0.0, 1.0, illuminatedFraction: 1.0);
        var atNew = NearBodyMeshBuilder.LightAt(1.6, 0.0, 0.0, 0.0, -1.0, illuminatedFraction: 0.0);

        Assert.True(atFull > 0.9f);
        Assert.True(atNew <= NearBodyMeshBuilder.NightSideLight + 0.001f);
    }

    [Fact]
    public void The_Farthest_Body_Is_Drawn_First_So_A_Passing_Moon_Crosses_In_Front()
    {
        var catalog = new NearBodyCatalog(
            NearBodyCatalog.CurrentSchemaVersion,
            HidesVanillaMoon: true,
            [
                Parent(0.0, 0.0) with { Id = "moon", AngularDiameterDeg = 0.6, Kind = NearBodyKind.Moon },
                Parent(0.0, 0.0) with { Id = "giant", AngularDiameterDeg = 22.0 }
            ]);

        var placed = NearBodyRenderModel.Place(catalog, 0.0, LatitudeDeg, 0.0, new SkyDirection(0, -1, 0));

        Assert.Equal(["giant", "moon"], placed.Select(static body => body.Body.Id));
    }

    /// <summary>
    /// At night the unlit half is a black bite out of the star field, which is what it really is. By
    /// day the same disc would be a hole in a blue sky, so the unlit part has to fade out instead.
    /// </summary>
    [Fact]
    public void The_Night_Side_Is_Solid_After_Dark_And_Fades_Out_By_Day()
    {
        var placed = new PlacedNearBody(
            Parent(0.0, 0.0),
            new SkyDirection(0.0, 1.0, 0.0),
            new SkyDirection(1.0, 0.0, 0.0),
            AltitudeDeg: 90.0,
            AngularDiameterDeg: 20.0,
            IlluminatedFraction: 0.5);

        var night = NearBodyMeshBuilder.Build([placed], 40f, 1, daylight: 0.0);
        var noon = NearBodyMeshBuilder.Build([placed], 40f, 1, daylight: 1.0);

        var subdivisions = NearBodyMeshBuilder.SubdivisionsFor(placed.Body.Face.DiscFraction);
        Assert.Equal(NearBodyMeshBuilder.VerticesFor(subdivisions), night.VerticesCount);
        Assert.Equal(NearBodyMeshBuilder.IndicesFor(subdivisions), night.IndicesCount);

        Assert.Equal(255, MinAlpha(night));
        Assert.True(MinAlpha(noon) < 40, $"the unlit side stayed at alpha {MinAlpha(noon)} in daylight");
        Assert.Equal(255, MaxAlpha(noon));
    }

    /// <summary>
    /// The terminator can only step as finely as the cells it is shaded across, and a ringed giant's
    /// globe is a fraction of its face. Counting cells over the whole face left six across the globe
    /// and drew the terminator as a staircase of blocks, so the count follows the globe instead.
    /// </summary>
    [Fact]
    public void A_Globe_Sharing_Its_Face_With_Rings_Still_Gets_A_Fine_Shading_Grid()
    {
        // A ring reaching 3.6 planet radii leaves the globe just over a quarter of the face.
        var withRings = NearBodyMeshBuilder.SubdivisionsFor(1.0 / 3.6);
        var bare = NearBodyMeshBuilder.SubdivisionsFor(1.0);

        // The cap on total cells bites before the full count is reached at this ring reach, which is
        // the intended trade: fifty-odd steps across the globe rather than the six it used to get.
        Assert.True(
            withRings * (1.0 / 3.6) >= 40.0,
            $"only {withRings / 3.6:0.0} cells landed across the globe");
        Assert.True(withRings > bare, "a ringed face needs more cells than a bare one");
        Assert.InRange(bare, NearBodyMeshBuilder.MinSubdivisions, NearBodyMeshBuilder.MaxSubdivisions);
        Assert.InRange(
            NearBodyMeshBuilder.SubdivisionsFor(0.01),
            NearBodyMeshBuilder.MinSubdivisions,
            NearBodyMeshBuilder.MaxSubdivisions);
    }

    /// <summary>
    /// By day a body is fogged out in proportion to how little light it sends, and the lit face
    /// sends plenty right out to its edge. Fading it by whether a point is lit instead asks a
    /// question with a cliff at the limb, and the answer drew the planet as a ring round the sky.
    /// </summary>
    [Fact]
    public void Daylight_Leaves_The_Lit_Face_Solid_And_Takes_The_Unlit_One()
    {
        var sunward = new SkyDirection(1.0, 0.0, 0.0);
        var full = Placed(direction: new SkyDirection(-1.0, 0.0, 0.0), sun: sunward, illuminated: 1.0);
        var quarter = Placed(direction: new SkyDirection(0.0, 0.0, 1.0), sun: sunward, illuminated: 0.5);

        // Face fully lit: solid at noon, middle and limb alike.
        Assert.Equal(255, MinAlpha(NearBodyMeshBuilder.Build([full], 40f, 1, daylight: 1.0)));

        // Half lit: the lit side still stands, the unlit side has all but gone.
        var half = NearBodyMeshBuilder.Build([quarter], 40f, 1, daylight: 1.0);
        Assert.Equal(255, MaxAlpha(half));
        Assert.True(MinAlpha(half) < 40, $"the unlit side stayed at alpha {MinAlpha(half)}");
    }

    /// <summary>
    /// A sphere's normal turns square to the eye exactly at its edge, with an infinite gradient.
    /// Shading the corners of cells cannot follow that, and the last ring of cells came out a
    /// scalloped fringe: the planet's edge drawn with teeth.
    /// </summary>
    [Fact]
    public void The_Very_Limb_Does_Not_Fall_Off_A_Cliff_No_Grid_Could_Draw()
    {
        // Sun behind the observer, so the whole disc is lit right out to its edge.
        var brightInside = NearBodyMeshBuilder.LightAt(0.96, 0.0, 0.0, 0.0, 1.0, 1.0);
        var brightLimb = NearBodyMeshBuilder.LightAt(1.0, 0.0, 0.0, 0.0, 1.0, 1.0);

        // Brightness may still fall toward the edge -- that is limb darkening, and it is meant to
        // be there -- but not by a step a cell cannot span.
        Assert.True(Math.Abs(brightInside - brightLimb) < 0.02f, $"{brightInside} to {brightLimb}");
        Assert.True(NearBodyMeshBuilder.LightAt(0.0, 0.0, 0.0, 0.0, 1.0, 1.0) > brightLimb);

        // And the night side is still the night side: the floor lifts the limb, not the shadow.
        Assert.True(NearBodyMeshBuilder.LightAt(-0.99, 0.0, 1.0, 0.0, 0.0, 0.5) < 0.1f);
    }

    /// <summary>
    /// The margin outside the globe is the ring plane, and it follows the whole body's phase. On an
    /// unlit limb that is nothing like the shading just inside the edge, and a step there is a step
    /// the grid draws across a cell whose inner half is globe: the dark limb came out fringed with
    /// lit teeth. Every body has this margin, ring or no ring, which is why the moons wore it too.
    /// </summary>
    [Fact]
    public void The_Ring_Plane_Leaves_The_Limb_At_The_Shading_The_Limb_Has()
    {
        // A gibbous phase: sun off to the right and behind us, so the left limb is night.
        const double SunRight = 0.8;
        const double SunToward = 0.6;
        const double Illuminated = 0.6;

        var insideTheLimb = NearBodyMeshBuilder.LightAt(-1.0, 0.0, SunRight, 0.0, SunToward, Illuminated);
        var justOutside = NearBodyMeshBuilder.LightAt(-1.0 - (2.0 / NearBodyMeshBuilder.CellsAcrossGlobe), 0.0, SunRight, 0.0, SunToward, Illuminated);

        Assert.True(insideTheLimb < 0.1f, $"the unlit limb came out at {insideTheLimb}");
        Assert.True(
            justOutside - insideTheLimb < 0.05f,
            $"the plane jumps to {justOutside} one cell out from a limb at {insideTheLimb}");

        // Out where a ring is actually drawn it has its phase in full, undimmed by the blend.
        var wellOut = NearBodyMeshBuilder.LightAt(1.6, 0.0, SunRight, 0.0, SunToward, Illuminated);
        Assert.Equal(NearBodyMeshBuilder.NightSideLight + (0.95f * (float)Illuminated), wellOut, 3);
    }

    /// <summary>
    /// A sibling closer to the parent than the observer is bound to it the way Venus is bound to the
    /// sun: it swings back and forth about the parent, reaches a greatest elongation of
    /// <c>asin(q)</c>, and never gets any further. Sending it round the whole sky at a flat rate --
    /// which is all an hour angle and a rate can do -- put moons in the midnight sky that physically
    /// cannot leave the planet's face.
    /// </summary>
    [Theory]
    [InlineData(0.26)]
    [InlineData(0.44)]
    [InlineData(0.9)]
    public void An_Inner_Sibling_Is_Penned_In_About_The_Parent(double distanceRatio)
    {
        const double AnchorDeg = 40.0;
        var greatestElongation = Math.Asin(distanceRatio) * 180.0 / Math.PI;
        var sibling = Sibling(new NearBodyOrbit(AnchorDeg, distanceRatio, PhaseDeg: 0.0, PhaseRateDegPerDay: 720.0));

        var reached = 0.0;
        for (var step = 0; step <= 2000; step++)
        {
            var offset = NearBodyRenderModel.HourAngleDeg(sibling, step / 1000.0) - AnchorDeg;
            Assert.InRange(Math.Abs(offset), 0.0, greatestElongation + 1e-9);
            reached = Math.Max(reached, Math.Abs(offset));
        }

        Assert.Equal(greatestElongation, reached, 1);
    }

    /// <summary>
    /// An outer sibling does go right round -- the observer laps the parent inside it -- but not at
    /// the flat rate the old model drew: it hangs near the parent while it is far off and swings
    /// through fastest when it passes closest.
    /// </summary>
    [Fact]
    public void An_Outer_Sibling_Goes_Right_Round_And_Fastest_When_It_Passes_Close()
    {
        var orbit = new NearBodyOrbit(AnchorHourAngleDeg: 0.0, DistanceRatio: 1.3, PhaseDeg: 0.0, PhaseRateDegPerDay: -360.0);
        var sibling = Sibling(orbit);

        // Phase zero puts it on the far side of the parent, so it draws opposite the parent.
        Assert.Equal(180.0, Math.Abs(NearBodyRenderModel.ElongationDeg(1.3, 0.0)), 9);

        // One full turn of phase carries the elongation one full turn round the sky.
        var swept = 0.0;
        var previous = NearBodyRenderModel.HourAngleDeg(sibling, 0.0);
        for (var step = 1; step <= 3600; step++)
        {
            var now = NearBodyRenderModel.HourAngleDeg(sibling, step / 3600.0);
            var delta = CelestialMath.NormalizeDegrees(now - previous);
            swept += delta > 180.0 ? delta - 360.0 : delta;
            previous = now;
        }

        // Phase runs backwards for an outer sibling, and the sky offset runs forwards: one turn west.
        Assert.Equal(360.0, swept, 6);

        // Closest approach is phase zero, opposite the parent; the swing there outruns the crawl it
        // does half a turn later, when it is round behind the parent and at its furthest.
        var nearRate = ElongationStep(1.3, 1.0, -1.0);
        var farRate = ElongationStep(1.3, 181.0, 179.0);
        Assert.True(nearRate > 3.0 * farRate, $"{nearRate} against {farRate}");
    }

    /// <summary>
    /// The sun is the limiting case of a sibling infinitely far out, and it has to come back as the
    /// plain one-turn-a-day it always was.
    /// </summary>
    [Fact]
    public void A_Sibling_Infinitely_Far_Out_Is_The_Sun()
    {
        var distant = new NearBodyOrbit(0.0, DistanceRatio: 1e7, PhaseDeg: 0.0, PhaseRateDegPerDay: -360.0);
        var sun = Sibling(distant);

        Assert.Equal(180.0, HourAngle(sun, 0.0), 4);
        Assert.Equal(270.0, HourAngle(sun, 0.25), 4);
        Assert.Equal(324.0, HourAngle(sun, 0.4), 4);
    }

    /// <summary>
    /// A sibling's distance swings over a synodic period, so a disc drawn at one fixed width is
    /// wrong at both ends of it. Inner or outer, the width has to follow the distance.
    /// </summary>
    [Fact]
    public void A_Siblings_Disc_Follows_How_Far_Off_It_Is()
    {
        Assert.Equal(0.75, NearBodyRenderModel.SeparationRatio(0.25, 0.0), 9);
        Assert.Equal(1.25, NearBodyRenderModel.SeparationRatio(0.25, 180.0), 9);
        Assert.Equal(0.5, NearBodyRenderModel.SeparationRatio(1.5, 0.0), 9);
        Assert.Equal(2.5, NearBodyRenderModel.SeparationRatio(1.5, 180.0), 9);

        var sibling = Sibling(new NearBodyOrbit(0.0, DistanceRatio: 0.25, PhaseDeg: 0.0, PhaseRateDegPerDay: 360.0))
            with { AngularDiameterDeg = 1.5 };
        var sun = new SkyDirection(0.0, -1.0, 0.0);

        var closest = NearBodyRenderModel.Place(sibling, 0.0, LatitudeDeg, 0.0, sun);
        var farthest = NearBodyRenderModel.Place(sibling, 0.5, LatitudeDeg, 0.0, sun);

        Assert.NotNull(closest);
        Assert.NotNull(farthest);
        Assert.Equal(1.5 / 0.75, closest.AngularDiameterDeg, 9);
        Assert.Equal(1.5 / 1.25, farthest.AngularDiameterDeg, 9);
    }

    /// <summary>
    /// Half of an inner sibling's circuit is spent behind the parent, where the parent's own globe
    /// hides it. Depth testing is off in this pass, so being hidden is a matter of being drawn
    /// first.
    /// </summary>
    [Fact]
    public void A_Sibling_Round_The_Far_Side_Is_Drawn_Behind_The_Parent()
    {
        var giant = Parent(0.0, 0.0) with { Id = "giant", AngularDiameterDeg = 22.0 };
        var sibling = Sibling(new NearBodyOrbit(0.0, DistanceRatio: 0.3, PhaseDeg: 180.0, PhaseRateDegPerDay: 360.0))
            with { Id = "sibling", AngularDiameterDeg = 0.6 };
        var catalog = new NearBodyCatalog(NearBodyCatalog.CurrentSchemaVersion, true, [sibling, giant]);
        var sun = new SkyDirection(0.0, -1.0, 0.0);

        // Phase 180 is round the far side of the parent: the parent is drawn over it.
        Assert.Equal(
            ["sibling", "giant"],
            NearBodyRenderModel.Place(catalog, 0.0, LatitudeDeg, 0.0, sun).Select(static body => body.Body.Id));

        // Half a turn later it is between the observer and the parent, transiting its face.
        Assert.Equal(
            ["giant", "sibling"],
            NearBodyRenderModel.Place(catalog, 0.5, LatitudeDeg, 0.0, sun).Select(static body => body.Body.Id));
    }

    private static double HourAngle(NearBodyEntry body, double totalDays)
        => CelestialMath.NormalizeDegrees(NearBodyRenderModel.HourAngleDeg(body, totalDays));

    private static double ElongationStep(double distanceRatio, double fromPhase, double toPhase)
    {
        var step = CelestialMath.NormalizeDegrees(
            NearBodyRenderModel.ElongationDeg(distanceRatio, fromPhase)
            - NearBodyRenderModel.ElongationDeg(distanceRatio, toPhase));
        return Math.Abs(step > 180.0 ? step - 360.0 : step);
    }

    private static NearBodyEntry Sibling(NearBodyOrbit orbit)
        => Parent(orbit.AnchorHourAngleDeg, 0.0) with
        {
            Id = "sibling",
            Kind = NearBodyKind.Moon,
            AngularDiameterDeg = 0.6,
            Orbit = orbit
        };

    private static PlacedNearBody Placed(SkyDirection direction, SkyDirection sun, double illuminated)
        => new(
            Parent(0.0, 0.0),
            direction,
            sun,
            AltitudeDeg: 45.0,
            AngularDiameterDeg: 20.0,
            IlluminatedFraction: illuminated);

    private static int MinAlpha(Vintagestory.API.Client.MeshData mesh)
        => AlphaBytes(mesh).Min();

    private static int MaxAlpha(Vintagestory.API.Client.MeshData mesh)
        => AlphaBytes(mesh).Max();

    private static IEnumerable<int> AlphaBytes(Vintagestory.API.Client.MeshData mesh)
    {
        for (var vertex = 0; vertex < mesh.VerticesCount; vertex++)
        {
            yield return mesh.Rgba[(vertex * 4) + 3];
        }
    }

    private static NearBodyEntry Parent(double hourAngleDeg, double rate)
        => new(
            "parent",
            "Warden",
            NearBodyKind.ParentPlanet,
            AngularDiameterDeg: 22.0,
            hourAngleDeg,
            rate,
            DeclinationDeg: 0.0,
            Brightness: 1.0,
            new NearBodyFace(2, new int[4], DiscFraction: 1.0));
}
