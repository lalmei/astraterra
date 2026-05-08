using AstraTerra.Client.Observation;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class TelescopeObservationStateTests
{
    [Theory]
    [InlineData(ObservationMode.Observe, ObservationMode.Draw)]
    [InlineData(ObservationMode.Draw, ObservationMode.Inspect)]
    [InlineData(ObservationMode.Inspect, ObservationMode.RemoveSegment)]
    [InlineData(ObservationMode.RemoveSegment, ObservationMode.Observe)]
    public void Mode_Cycle_Follows_Agreed_Order(ObservationMode current, ObservationMode expectedNext)
    {
        Assert.Equal(expectedNext, TelescopeObservationState.NextMode(current));
    }

    [Fact]
    public void ScopeZoom_Defaults_To_Five_Steps()
    {
        TelescopeScopeState.Begin();

        TelescopeScopeState.ScrollZoom(99);

        Assert.Equal(TelescopeObservationState.MaxZoomStep, TelescopeScopeState.MaxZoomStep);
        Assert.Equal(5, TelescopeScopeState.ZoomStep);
        Assert.Equal(0.12f, TelescopeScopeState.GetFovMultiplier(), precision: 4);
        TelescopeScopeState.End();
    }

    [Fact]
    public void ScopeZoom_Can_Use_Ten_Step_Higher_Zoom_Telescope()
    {
        TelescopeScopeState.Begin(
            TelescopeObservationState.PrecisionMaxZoomStep,
            TelescopeObservationState.PrecisionMaxZoomFovMultiplier);

        TelescopeScopeState.ScrollZoom(99);

        Assert.Equal(10, TelescopeScopeState.MaxZoomStep);
        Assert.Equal(10, TelescopeScopeState.ZoomStep);
        Assert.Equal(0.06f, TelescopeScopeState.GetFovMultiplier(), precision: 4);
        TelescopeScopeState.End();
    }

    [Fact]
    public void ScopeZoom_Clamps_Back_When_Returning_To_Five_Step_Telescope()
    {
        TelescopeScopeState.Begin(
            TelescopeObservationState.PrecisionMaxZoomStep,
            TelescopeObservationState.PrecisionMaxZoomFovMultiplier);
        TelescopeScopeState.ScrollZoom(99);

        TelescopeScopeState.Begin();

        Assert.Equal(TelescopeObservationState.MaxZoomStep, TelescopeScopeState.MaxZoomStep);
        Assert.Equal(5, TelescopeScopeState.ZoomStep);
        TelescopeScopeState.End();
    }
}
