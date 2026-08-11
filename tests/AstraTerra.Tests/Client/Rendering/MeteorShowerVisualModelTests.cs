using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class MeteorShowerVisualModelTests
{
    [Fact]
    public void Published_Rate_Uses_A_Real_Observation_Hour()
    {
        var model = new MeteorShowerVisualModel(seed: 42);
        var reading = Reading("GEM", observedHourlyRate: 120.0);

        for (var frame = 0; frame < 119; frame++)
        {
            model.Advance(0.25, [reading]);
        }

        Assert.Empty(model.ActiveStreaks);

        model.Advance(0.25, [reading]);

        Assert.Single(model.ActiveStreaks);
        Assert.Equal("GEM", model.ActiveStreaks[0].ShowerId);
    }

    [Fact]
    public void Debug_Multiplier_Can_Reproduce_The_Original_Accelerated_Rate()
    {
        var model = new MeteorShowerVisualModel(seed: 42);
        var reading = Reading("GEM", observedHourlyRate: 120.0);

        for (var frame = 0; frame < 4; frame++)
        {
            model.Advance(0.25, [reading], debugRateMultiplier: 30.0);
        }

        Assert.Single(model.ActiveStreaks);
    }

    [Fact]
    public void Zero_Observed_Rate_Never_Spawns_And_Clears_Spawn_Credit()
    {
        var model = new MeteorShowerVisualModel(seed: 12);
        var active = Reading("PER", observedHourlyRate: 120.0);
        var inactive = Reading("PER", observedHourlyRate: 0.0);

        model.Advance(0.25, [active]);
        model.Advance(0.25, [active]);
        model.Advance(0.25, [inactive]);
        model.Advance(0.25, [active]);
        model.Advance(0.25, [active]);

        Assert.Empty(model.ActiveStreaks);
    }

    [Fact]
    public void Spawned_Streaks_Are_Above_The_Horizon_And_Use_Unit_Directions()
    {
        var model = new MeteorShowerVisualModel(seed: 7);
        var reading = Reading("PER", observedHourlyRate: 480.0, azimuthDeg: 45.0, altitudeDeg: 50.0);

        model.Advance(0.25, [reading], debugRateMultiplier: 30.0);

        var streak = Assert.Single(model.ActiveStreaks);
        Assert.True(streak.InitialCenter.Y > 0.0);
        Assert.Equal(1.0, Length(streak.InitialCenter), 10);
        Assert.Equal(1.0, Length(streak.InitialTangent), 10);
        Assert.Equal(0.0, Dot(streak.InitialCenter, streak.InitialTangent), 10);
    }

    [Fact]
    public void Trails_Far_From_The_Radiant_Are_Longer_Than_Nearby_Trails()
    {
        var near = MeteorShowerVisualModel.CalculateAngularLengthDeg(8.0);
        var middle = MeteorShowerVisualModel.CalculateAngularLengthDeg(35.0);
        var far = MeteorShowerVisualModel.CalculateAngularLengthDeg(70.0);

        Assert.True(near < middle);
        Assert.True(middle < far);
    }

    [Fact]
    public void Streaks_Grow_Move_And_Fade_Out()
    {
        var model = new MeteorShowerVisualModel(seed: 99);
        var reading = Reading("GEM", observedHourlyRate: 480.0);

        var birthFrame = Assert.Single(model.Advance(0.25, [reading], debugRateMultiplier: 30.0));
        var movingFrame = Assert.Single(model.Advance(0.10, [Reading("GEM", observedHourlyRate: 0.0)]));

        Assert.True(movingFrame.AngularLengthDeg > birthFrame.AngularLengthDeg);
        Assert.True(Dot(birthFrame.Center, movingFrame.Center) < 1.0);
        Assert.True(movingFrame.Alpha > birthFrame.Alpha);

        for (var frame = 0; frame < 8; frame++)
        {
            model.Advance(0.25, [Reading("GEM", observedHourlyRate: 0.0)]);
        }

        Assert.Empty(model.ActiveStreaks);
    }

    private static MeteorShowerReading Reading(
        string id,
        double observedHourlyRate,
        double azimuthDeg = 180.0,
        double altitudeDeg = 70.0)
    {
        var shower = new MeteorShowerEntry(id, id, 0.0, 0.0, 0.0, 10.0, observedHourlyRate);
        return new MeteorShowerReading(
            shower,
            0.0,
            azimuthDeg,
            altitudeDeg,
            observedHourlyRate > 0.0 ? 1.0 : 0.0,
            observedHourlyRate > 0.0 ? 1.0 : 0.0,
            1.0,
            1.0,
            observedHourlyRate);
    }

    private static double Length(MeteorSkyDirection value)
        => Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));

    private static double Dot(MeteorSkyDirection first, MeteorSkyDirection second)
        => (first.X * second.X) + (first.Y * second.Y) + (first.Z * second.Z);
}
