using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class CometRenderModelTests
{
    private const int DaysPerYear = 100;

    private static CometEntry Comet(double rightAscensionDeg = 0.0, double declinationDeg = 0.0)
        => new(
            "test",
            "Test",
            PeriodYears: 10.0,
            FirstPerihelionYear: 2.0,
            WindowHalfWidthYears: 0.1,
            PeakMagnitude: -1.0,
            EdgeMagnitude: 8.0,
            BrighteningExponent: 0.5,
            PeakTailLengthDeg: 20.0,
            Path: [new CometPathKeyframe(0.0, rightAscensionDeg, declinationDeg)],
            TintR: 1f,
            TintG: 1f,
            TintB: 1f);

    /// <summary>
    /// The one piece of comet behaviour a player can check against what they know: the tail points
    /// away from the sun, not along the comet's motion.
    /// </summary>
    [Fact]
    public void The_Tail_Points_Directly_Away_From_The_Sun()
    {
        // Sun on the horizon due east, comet overhead: the tail must run due west along the sky.
        var coma = new SkyDirection(0, 1, 0);
        var sun = new SkyDirection(1, 0, 0);

        var axis = CometRenderModel.GetTailAxis(coma, sun);

        Assert.Equal(-1.0, axis.X, 6);
        Assert.Equal(0.0, axis.Y, 6);
        Assert.Equal(0.0, axis.Z, 6);
    }

    [Fact]
    public void The_Tail_Axis_Lies_Along_The_Sky_At_The_Coma()
    {
        var coma = Normalize(new SkyDirection(0.3, 0.8, -0.5));
        var sun = Normalize(new SkyDirection(-0.7, -0.2, 0.6));

        var axis = CometRenderModel.GetTailAxis(coma, sun);

        // Perpendicular to the coma direction, so rotating along it stays on the sphere...
        Assert.Equal(0.0, (axis.X * coma.X) + (axis.Y * coma.Y) + (axis.Z * coma.Z), 6);

        // ...and unit length, so the rotation the mesh builder does covers the angle it means to.
        Assert.Equal(1.0, Math.Sqrt((axis.X * axis.X) + (axis.Y * axis.Y) + (axis.Z * axis.Z)), 6);

        // Leaning away from the sun rather than towards it.
        Assert.True((axis.X * sun.X) + (axis.Y * sun.Y) + (axis.Z * sun.Z) < 0);
    }

    /// <summary>
    /// The tail swings right around as the comet rounds perihelion, because the sun moves and the
    /// tail follows it. Two sun positions either side must give opposite tails.
    /// </summary>
    [Fact]
    public void The_Tail_Swings_As_The_Sun_Moves()
    {
        var coma = new SkyDirection(0, 1, 0);

        var before = CometRenderModel.GetTailAxis(coma, new SkyDirection(1, 0, 0));
        var after = CometRenderModel.GetTailAxis(coma, new SkyDirection(-1, 0, 0));

        Assert.Equal(-before.X, after.X, 6);
        Assert.Equal(-before.Z, after.Z, 6);
    }

    /// <summary>
    /// A comet sitting on the sun has a tail pointed straight at or away from the observer, which has
    /// no direction across the sky at all. It must still produce a usable axis rather than a zero
    /// vector the mesh builder would divide by.
    /// </summary>
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, -1, 0)]
    public void A_Comet_On_The_Sun_Still_Gets_A_Usable_Axis(double x, double y, double z)
    {
        var coma = new SkyDirection(0, 1, 0);

        var axis = CometRenderModel.GetTailAxis(coma, new SkyDirection(x, y, z));

        Assert.Equal(1.0, Math.Sqrt((axis.X * axis.X) + (axis.Y * axis.Y) + (axis.Z * axis.Z)), 6);
        Assert.Equal(0.0, (axis.X * coma.X) + (axis.Y * coma.Y) + (axis.Z * coma.Z), 6);
    }

    [Fact]
    public void A_Comet_That_Is_Not_Due_Is_Not_Projected()
    {
        var model = new CometRenderModel(new CometCatalog(1, [Comet()]), DaysPerYear);

        // World year 5: nowhere near the window at 2.0, whatever the sky is doing.
        var away = model.ProjectVisibleComets(
            5.0 * DaysPerYear,
            latitudeDeg: 0.0,
            localSiderealDeg: 0.0,
            brightnessBias: 1.0,
            sunDirection: new SkyDirection(0, -1, 0));

        Assert.Empty(away);
    }

    [Fact]
    public void A_Comet_Mid_Apparition_And_Above_The_Horizon_Is_Projected_With_Its_Tail()
    {
        var model = new CometRenderModel(new CometCatalog(1, [Comet(rightAscensionDeg: 0.0, declinationDeg: 90.0)]), DaysPerYear);

        // Declination 90 is the north celestial pole: up at any northern latitude, whatever the hour.
        var visible = model.ProjectVisibleComets(
            2.0 * DaysPerYear,
            latitudeDeg: 50.0,
            localSiderealDeg: 0.0,
            brightnessBias: 1.0,
            sunDirection: new SkyDirection(0, -1, 0));

        var comet = Assert.Single(visible);
        Assert.Equal(1.0, comet.Prominence, 6);
        Assert.Equal(20.0, comet.TailLengthDeg, 6);
        Assert.Equal(-1.0, comet.VisualMagnitude, 6);
    }

    /// <summary>
    /// Below the horizon a comet is dropped by exactly the same cutoff every other body goes
    /// through, rather than by a rule of its own.
    /// </summary>
    [Fact]
    public void A_Comet_Below_The_Horizon_Is_Dropped()
    {
        var model = new CometRenderModel(new CometCatalog(1, [Comet(rightAscensionDeg: 0.0, declinationDeg: 90.0)]), DaysPerYear);

        var visible = model.ProjectVisibleComets(
            2.0 * DaysPerYear,
            latitudeDeg: -50.0,
            localSiderealDeg: 0.0,
            brightnessBias: 1.0,
            sunDirection: new SkyDirection(0, -1, 0));

        Assert.Empty(visible);
    }

    private static SkyDirection Normalize(SkyDirection direction)
    {
        var length = Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));
        return new SkyDirection(direction.X / length, direction.Y / length, direction.Z / length);
    }
}
