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
    /// Daylight fades the unlit half out, and it has to leave the lit half's own edge alone. Fading
    /// by brightness took the limb's darkening with it and ate a translucent ring out of the
    /// planet's silhouette, which reads as a hole rather than as a shadow.
    /// </summary>
    [Fact]
    public void Daylight_Fades_The_Unlit_Half_Without_Thinning_The_Lit_Limb()
    {
        // Sun straight behind the observer: the whole face is lit, limb included.
        var middle = NearBodyMeshBuilder.LitFractionAt(0.0, 0.0, 0.0, 0.0, 1.0, 1.0);
        var limb = NearBodyMeshBuilder.LitFractionAt(0.96, 0.0, 0.0, 0.0, 1.0, 1.0);

        Assert.Equal(1.0f, middle, 3);
        Assert.Equal(1.0f, limb, 3);

        // The brightness at that same limb is lower -- that is limb darkening, and it belongs to
        // the colour rather than to the silhouette.
        Assert.True(NearBodyMeshBuilder.LightAt(0.96, 0.0, 0.0, 0.0, 1.0, 1.0) < 0.9f);

        // The night side still goes: that is the half a bright sky should swallow.
        Assert.Equal(0.0f, NearBodyMeshBuilder.LitFractionAt(-0.6, 0.0, 1.0, 0.0, 0.0, 0.5), 3);
    }

    /// <summary>
    /// A sphere's normal turns square to the eye exactly at its edge, with an infinite gradient.
    /// Shading the corners of cells cannot follow that, and the last ring of cells came out a
    /// scalloped half-transparent fringe: the planet's edge drawn with teeth.
    /// </summary>
    [Fact]
    public void The_Very_Limb_Does_Not_Fall_Off_A_Cliff_No_Grid_Could_Draw()
    {
        // Sun behind the observer, so the whole disc is lit right out to its edge.
        var justInside = NearBodyMeshBuilder.LitFractionAt(0.96, 0.0, 0.0, 0.0, 1.0, 1.0);
        var atTheLimb = NearBodyMeshBuilder.LitFractionAt(1.0, 0.0, 0.0, 0.0, 1.0, 1.0);

        Assert.Equal(1.0f, justInside, 3);
        Assert.Equal(1.0f, atTheLimb, 3);

        // Brightness may still fall toward the edge -- that is limb darkening, and it is meant to
        // be there -- but not by a step a cell cannot span.
        var brightInside = NearBodyMeshBuilder.LightAt(0.96, 0.0, 0.0, 0.0, 1.0, 1.0);
        var brightLimb = NearBodyMeshBuilder.LightAt(1.0, 0.0, 0.0, 0.0, 1.0, 1.0);
        Assert.True(Math.Abs(brightInside - brightLimb) < 0.02f, $"{brightInside} to {brightLimb}");

        // And the middle is still brighter than the edge.
        Assert.True(NearBodyMeshBuilder.LightAt(0.0, 0.0, 0.0, 0.0, 1.0, 1.0) > brightLimb);
    }

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
