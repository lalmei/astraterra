using AstraTerra.Astronomy;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class LongitudeAwareSunControllerTests
{
    [Fact]
    public void Configuration_Before_Lifecycle_Waits_And_Then_Chains_The_Base_Delegate()
    {
        float observedDayRel = -1;
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, dayRel) =>
        {
            observedDayRel = dayRel;
            return new SolarSphericalCoords(dayRel, 2f);
        };
        var controller = new LongitudeAwareSunController(_ => 90.0);

        var beforeReady = controller.Configure(longitudeAwareSunEnabled: true, baseDelegate);
        var ready = controller.MarkLifecycleReady(baseDelegate);
        var result = ready.Delegate!(12, 34, 0.25f, 0.9f);

        Assert.Equal(SunDelegateUpdateKind.None, beforeReady.Kind);
        Assert.Equal(SunDelegateUpdateKind.Installed, ready.Kind);
        Assert.Equal(0.15f, observedDayRel, 5);
        Assert.Equal(observedDayRel, result.ZenithAngle);
        Assert.Equal(2f, result.AzimuthAngle);
    }

    [Fact]
    public void Lifecycle_Before_Server_Configuration_Also_Installs()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        var controller = new LongitudeAwareSunController(_ => -45.0);

        var beforeConfig = controller.MarkLifecycleReady(baseDelegate);
        var configured = controller.Configure(longitudeAwareSunEnabled: true, baseDelegate);

        Assert.Equal(SunDelegateUpdateKind.None, beforeConfig.Kind);
        Assert.Equal(SunDelegateUpdateKind.Installed, configured.Kind);
    }

    [Fact]
    public void Disabled_Server_Policy_Leaves_The_Base_Delegate_Untouched()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        var controller = new LongitudeAwareSunController(_ => 90.0);

        controller.MarkLifecycleReady(baseDelegate);
        var update = controller.Configure(longitudeAwareSunEnabled: false, baseDelegate);

        Assert.Equal(SunDelegateUpdateKind.None, update.Kind);
        Assert.Same(baseDelegate, update.Delegate);
    }

    [Fact]
    public void Reset_Restores_Only_The_Delegate_This_Controller_Installed()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        SolarSphericalCoordsDelegate laterReplacement = (_, _, _, _) => new SolarSphericalCoords(3f, 4f);
        var controller = new LongitudeAwareSunController(_ => 90.0);
        controller.Configure(longitudeAwareSunEnabled: true, baseDelegate);
        var installed = controller.MarkLifecycleReady(baseDelegate);

        var leftAlone = controller.Reset(laterReplacement);

        Assert.Equal(SunDelegateUpdateKind.None, leftAlone.Kind);
        Assert.Same(laterReplacement, leftAlone.Delegate);

        var secondController = new LongitudeAwareSunController(_ => 90.0);
        secondController.Configure(longitudeAwareSunEnabled: true, baseDelegate);
        var secondInstalled = secondController.MarkLifecycleReady(baseDelegate);
        var restored = secondController.Reset(secondInstalled.Delegate);

        Assert.Equal(SunDelegateUpdateKind.Restored, restored.Kind);
        Assert.Same(baseDelegate, restored.Delegate);
    }

    [Fact]
    public void Calendar_Placeholder_Is_Not_Chained()
    {
        SolarSphericalCoordsDelegate placeholder = (_, _, _, dayRel) =>
            new SolarSphericalCoords((float)(Math.PI * 2 * dayRel - Math.PI), 0f);
        var controller = new LongitudeAwareSunController(_ => 90.0);
        controller.Configure(longitudeAwareSunEnabled: true, placeholder);

        var update = controller.MarkLifecycleReady(placeholder);

        Assert.Equal(SunDelegateUpdateKind.WaitingForBaseDelegate, update.Kind);
        Assert.Same(placeholder, update.Delegate);
    }

    [Fact]
    public void The_Sky_Only_Counts_The_Wrapper_As_Installed_While_The_Calendar_Still_Holds_It()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        SolarSphericalCoordsDelegate laterMod = (_, _, _, _) => new SolarSphericalCoords(3f, 4f);
        var controller = new LongitudeAwareSunController(_ => 90.0);

        Assert.False(controller.IsInstalledOn(baseDelegate));

        controller.Configure(longitudeAwareSunEnabled: true, baseDelegate);
        var installed = controller.MarkLifecycleReady(baseDelegate);

        Assert.True(controller.IsInstalledOn(installed.Delegate));

        // Another mod assigning the property wins, and the star field must stop shifting with it.
        Assert.False(controller.IsInstalledOn(laterMod));

        controller.Configure(longitudeAwareSunEnabled: false, installed.Delegate);
        Assert.False(controller.IsInstalledOn(installed.Delegate));
    }

    [Fact]
    public void Separate_Controllers_Keep_Client_And_Server_Delegate_State_Independent()
    {
        float clientDayRel = -1;
        float serverDayRel = -1;
        SolarSphericalCoordsDelegate clientBase = (_, _, _, dayRel) =>
        {
            clientDayRel = dayRel;
            return default;
        };
        SolarSphericalCoordsDelegate serverBase = (_, _, _, dayRel) =>
        {
            serverDayRel = dayRel;
            return default;
        };
        var client = new LongitudeAwareSunController(_ => 90.0);
        var server = new LongitudeAwareSunController(_ => -90.0);

        client.Configure(true, clientBase);
        server.Configure(true, serverBase);
        var clientInstalled = client.MarkLifecycleReady(clientBase);
        var serverInstalled = server.MarkLifecycleReady(serverBase);
        clientInstalled.Delegate!(0, 0, 0, 0.5f);
        serverInstalled.Delegate!(0, 0, 0, 0.5f);

        Assert.Equal(0.75f, clientDayRel, 5);
        Assert.Equal(0.25f, serverDayRel, 5);
    }

    /// <summary>
    /// A world's tilt is reason enough to take the delegate over, even where the server has the
    /// longitude term switched off. The two are independent settings on one wrapper.
    /// </summary>
    [Fact]
    public void A_World_Tilt_Installs_The_Wrapper_With_Longitude_Off()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        var controller = new LongitudeAwareSunController(_ => 90.0, _ => 0.0);

        try
        {
            controller.MarkLifecycleReady(baseDelegate);
            var withoutTilt = controller.Configure(longitudeAwareSunEnabled: false, baseDelegate);
            var withTilt = controller.SetWorldTilt(12.0, withoutTilt.Delegate);

            Assert.Equal(SunDelegateUpdateKind.None, withoutTilt.Kind);
            Assert.Equal(SunDelegateUpdateKind.Installed, withTilt.Kind);
            Assert.False(controller.LongitudeEnabled);
            Assert.Equal(12.0, WorldTilt.CurrentDeg, 9);
        }
        finally
        {
            controller.Reset(null);
        }
    }

    /// <summary>
    /// With a tilt installed, the wrapper answers with its own sun rather than passing the question
    /// down: vanilla's delegate cannot swing further than vanilla's own tilt, so a substitution has
    /// to rebuild the answer.
    /// </summary>
    [Fact]
    public void A_Tilted_World_Stops_Asking_The_Survival_Delegate()
    {
        var asked = 0;
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) =>
        {
            asked++;
            return new SolarSphericalCoords(1f, 2f);
        };
        var controller = new LongitudeAwareSunController(_ => 0.0, _ => 0.0);

        try
        {
            controller.MarkLifecycleReady(baseDelegate);
            controller.Configure(longitudeAwareSunEnabled: false, baseDelegate);
            var installed = controller.SetWorldTilt(40.0, baseDelegate);

            // Installing probes the candidate delegate to tell a real one from the calendar's own
            // placeholder, so the count starts here rather than at zero.
            asked = 0;
            var tipped = installed.Delegate!(0, 0, 0.5f, 0.5f);

            Assert.Equal(0, asked);
            Assert.NotEqual(1f, tipped.ZenithAngle);
        }
        finally
        {
            controller.Reset(null);
        }
    }

    /// <summary>
    /// A server that refuses generated tilts is obeyed, and the world stays on Earth's axis even
    /// though a generator handed one over. The refusal reaches clients on the same packet the
    /// longitude policy travels on.
    /// </summary>
    [Fact]
    public void A_Server_That_Refuses_Generated_Tilts_Is_Obeyed()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        var controller = new LongitudeAwareSunController(_ => 0.0, _ => 0.0);

        try
        {
            controller.MarkLifecycleReady(baseDelegate);
            controller.Configure(longitudeAwareSunEnabled: false, baseDelegate, worldTiltEnabled: false);
            var refused = controller.SetWorldTilt(40.0, baseDelegate);

            Assert.Equal(SunDelegateUpdateKind.None, refused.Kind);
            Assert.Null(controller.WorldTiltDeg);
            Assert.Equal(CelestialMath.MeanObliquityDeg, WorldTilt.CurrentDeg, 9);
        }
        finally
        {
            controller.Reset(null);
        }
    }

    /// <summary>
    /// Clearing the tilt puts the survival delegate back in charge, and puts the world back on
    /// Earth's axis for everything that reads it.
    /// </summary>
    [Fact]
    public void Clearing_The_Tilt_Returns_The_World_To_Earths_Axis()
    {
        SolarSphericalCoordsDelegate baseDelegate = (_, _, _, _) => new SolarSphericalCoords(1f, 2f);
        var controller = new LongitudeAwareSunController(_ => 0.0, _ => 0.0);

        try
        {
            controller.MarkLifecycleReady(baseDelegate);
            controller.Configure(longitudeAwareSunEnabled: false, baseDelegate);
            var installed = controller.SetWorldTilt(40.0, baseDelegate);
            var cleared = controller.SetWorldTilt(null, installed.Delegate);

            Assert.Equal(SunDelegateUpdateKind.Restored, cleared.Kind);
            Assert.Same(baseDelegate, cleared.Delegate);
            Assert.Equal(CelestialMath.MeanObliquityDeg, WorldTilt.CurrentDeg, 9);
        }
        finally
        {
            controller.Reset(null);
        }
    }
}
