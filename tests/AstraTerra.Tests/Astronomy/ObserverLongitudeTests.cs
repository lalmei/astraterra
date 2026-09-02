using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// Longitude may only enter the sky while the sun the player can see has it too. Everything the mod
/// draws is measured against that sun, so a star field that shifted while the sun stayed put would
/// transit at an hour the sun disagrees with.
/// </summary>
[Collection("ObserverLongitude")]
public sealed class ObserverLongitudeTests : IDisposable
{
    public ObserverLongitudeTests() => ObserverLongitude.Reset();

    public void Dispose() => ObserverLongitude.Reset();

    [Fact]
    public void Nothing_Registered_Reads_As_A_Sun_That_Ignores_Longitude()
    {
        Assert.False(ObserverLongitude.SunFollowsLongitude);
    }

    [Fact]
    public void The_Registered_Installer_Decides_Every_Time_It_Is_Asked()
    {
        var installed = false;
        ObserverLongitude.FollowSun(() => installed);

        Assert.False(ObserverLongitude.SunFollowsLongitude);

        installed = true;
        Assert.True(ObserverLongitude.SunFollowsLongitude);

        // A later mod taking the solar delegate back must be noticed on the next frame, not cached.
        installed = false;
        Assert.False(ObserverLongitude.SunFollowsLongitude);
    }

    [Fact]
    public void Resetting_Returns_To_A_Single_World_Time()
    {
        ObserverLongitude.FollowSun(() => true);
        ObserverLongitude.Reset();

        Assert.False(ObserverLongitude.SunFollowsLongitude);
    }
}
