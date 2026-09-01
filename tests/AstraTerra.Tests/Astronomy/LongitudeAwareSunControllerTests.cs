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
}
