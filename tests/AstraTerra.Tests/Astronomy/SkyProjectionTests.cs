using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class SkyProjectionTests
{
    [Fact]
    public void A_Rendered_Body_Is_A_Value_Type_To_Avoid_Per_Frame_Heap_Allocation()
    {
        Assert.True(typeof(RenderedBody).IsValueType);
    }

    [Fact]
    public void A_Moving_Body_Lands_Exactly_Where_A_Star_At_The_Same_Position_Does()
    {
        var coordinates = new EquatorialCoordinates(37.9546, 89.2641);

        var body = SkyProjection.Project(coordinates, visualMagnitude: 1.98, latitudeDeg: 45, localSiderealDeg: 60, brightnessBias: 1);
        var star = StarRenderModel.Project(
            new StarCatalogEntry(11767, coordinates.RightAscensionDeg, coordinates.DeclinationDeg, 1.98, 0.60, true),
            latitudeDeg: 45,
            localSiderealDeg: 60,
            brightnessBias: 1);

        Assert.NotNull(body);
        Assert.NotNull(star);
        Assert.Equal(star!.Value.Body, body.Value);
    }

    [Fact]
    public void Project_Carries_The_Position_Through_For_Bodies_That_Move()
    {
        var coordinates = new EquatorialCoordinates(123.5, -12.25);

        var body = SkyProjection.Project(coordinates, visualMagnitude: 2, latitudeDeg: 0, localSiderealDeg: 123.5, brightnessBias: 1);

        // A planet's right ascension is only known at a world time, so the astrolabe and sextant
        // need it back off the projection rather than from a catalog.
        Assert.True(body.HasValue);
        Assert.Equal(coordinates, body.Value.Coordinates);
    }

    [Fact]
    public void Project_Drops_A_Body_Below_The_Visual_Horizon()
    {
        var body = SkyProjection.Project(
            new EquatorialCoordinates(120, 0),
            visualMagnitude: 1,
            latitudeDeg: 0,
            localSiderealDeg: 0,
            brightnessBias: 1);

        Assert.Null(body);
    }

    [Fact]
    public void Project_Keeps_A_Body_Slightly_Below_The_Mathematical_Horizon()
    {
        var body = SkyProjection.Project(
            new EquatorialCoordinates(100, 0),
            visualMagnitude: 1,
            latitudeDeg: 0,
            localSiderealDeg: 0,
            brightnessBias: 1);

        Assert.True(body.HasValue);
        Assert.InRange(body.Value.AltitudeDeg, -11.0, -9.0);
        Assert.True(body.Value.Brightness > 0);
    }

    [Fact]
    public void Project_Dims_A_Body_Near_The_Horizon()
    {
        var high = SkyProjection.Project(new EquatorialCoordinates(0, 0), 1, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);
        var low = SkyProjection.Project(new EquatorialCoordinates(84, 0), 1, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(high.HasValue);
        Assert.True(low.HasValue);
        Assert.True(high.Value.Brightness > low.Value.Brightness);
    }

    [Fact]
    public void Project_Produces_A_Unit_Direction_With_North_On_Negative_Z()
    {
        var body = SkyProjection.Project(new EquatorialCoordinates(0, 89.0), 2, latitudeDeg: 45, localSiderealDeg: 0, brightnessBias: 1);

        Assert.True(body.HasValue);
        var projectedBody = body.Value;
        var length = Math.Sqrt(
            (projectedBody.DirectionX * projectedBody.DirectionX) +
            (projectedBody.DirectionY * projectedBody.DirectionY) +
            (projectedBody.DirectionZ * projectedBody.DirectionZ));
        Assert.Equal(1.0, length, precision: 6);
        Assert.True(projectedBody.DirectionZ < 0);
        Assert.InRange(projectedBody.AzimuthDeg, 0.0, 1.0);
    }

    [Fact]
    public void Project_Clamps_Brightness_And_Keeps_Size_Independent_Of_The_Bias()
    {
        var night = SkyProjection.Project(new EquatorialCoordinates(0, 0), -2, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 100);
        var twilight = SkyProjection.Project(new EquatorialCoordinates(0, 0), -2, latitudeDeg: 0, localSiderealDeg: 0, brightnessBias: 0.3);

        Assert.True(night.HasValue);
        Assert.True(twilight.HasValue);
        Assert.InRange(night.Value.Brightness, 0.0, 1.0);
        Assert.True(night.Value.Brightness > twilight.Value.Brightness);
        Assert.Equal(night.Value.Size, twilight.Value.Size);
    }

    [Fact]
    public void A_Brighter_Magnitude_Reads_Brighter_And_Larger()
    {
        Assert.True(SkyProjection.GetBrightnessFromMagnitude(-1.0) > SkyProjection.GetBrightnessFromMagnitude(2.5));
        Assert.True(SkyProjection.GetBrightnessFromMagnitude(2.5) > SkyProjection.GetBrightnessFromMagnitude(6.0));
        Assert.True(SkyProjection.GetSizeFromMagnitude(-1.0) > SkyProjection.GetSizeFromMagnitude(2.5));
        Assert.True(SkyProjection.GetSizeFromMagnitude(2.5) > SkyProjection.GetSizeFromMagnitude(6.0));
    }

    [Fact]
    public void The_Horizon_Fade_Runs_From_Nothing_At_The_Cutoff_To_Full_At_The_Band()
    {
        Assert.Equal(0.0, SkyProjection.GetHorizonFadeFactor(SkyProjection.DefaultVisualHorizonCutoffDeg), 9);
        Assert.Equal(1.0, SkyProjection.GetHorizonFadeFactor(SkyProjection.DefaultHorizonFadeBandDeg), 9);
        Assert.Equal(1.0, SkyProjection.GetHorizonFadeFactor(90.0), 9);
        Assert.InRange(SkyProjection.GetHorizonFadeFactor(0.0), 0.0, 1.0);
    }

    [Fact]
    public void The_Standalone_Direction_Matches_The_One_A_Projection_Builds()
    {
        var coordinates = new EquatorialCoordinates(212.5, -34.0);

        var body = SkyProjection.Project(coordinates, 1, latitudeDeg: -20, localSiderealDeg: 200, brightnessBias: 1);
        var (x, y, z) = SkyProjection.GetWorldDirection(coordinates, latitudeDeg: -20, localSiderealDeg: 200);

        Assert.True(body.HasValue);
        Assert.Equal(body.Value.DirectionX, x, precision: 12);
        Assert.Equal(body.Value.DirectionY, y, precision: 12);
        Assert.Equal(body.Value.DirectionZ, z, precision: 12);
    }
}
