using AstraTerra.Observation;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class LocalObservationGateTests
{
    private const string LocalPlayer = "local-player-uid";
    private const string OtherPlayer = "other-player-uid";

    [Fact]
    public void Local_Player_On_The_Client_Drives_The_Hud()
    {
        Assert.True(LocalObservationGate.DrivesLocalHud(EnumAppSide.Client, LocalPlayer, LocalPlayer));
    }

    [Fact]
    public void Server_Side_Never_Drives_The_Hud()
    {
        // A hosted world runs its server inside the host's own client process, so an unguarded
        // server-side interaction would flip the host's overlay when a guest raised a telescope.
        Assert.False(LocalObservationGate.DrivesLocalHud(EnumAppSide.Server, LocalPlayer, LocalPlayer));
        Assert.False(LocalObservationGate.DrivesLocalHud(EnumAppSide.Server, OtherPlayer, LocalPlayer));
    }

    [Fact]
    public void Another_Players_Interaction_Does_Not_Drive_The_Hud()
    {
        Assert.False(LocalObservationGate.DrivesLocalHud(EnumAppSide.Client, OtherPlayer, LocalPlayer));
    }

    [Theory]
    [InlineData(null, LocalPlayer)]
    [InlineData("", LocalPlayer)]
    [InlineData(LocalPlayer, null)]
    [InlineData(null, null)]
    public void Unidentified_Players_Do_Not_Drive_The_Hud(string? interactingPlayerUid, string? localPlayerUid)
    {
        // A non-player entity, or a client with no resolved local player, must not be mistaken for
        // a match — including the case where both sides are absent.
        Assert.False(LocalObservationGate.DrivesLocalHud(EnumAppSide.Client, interactingPlayerUid, localPlayerUid));
    }

    [Fact]
    public void Player_Identity_Is_Compared_Exactly()
    {
        Assert.False(LocalObservationGate.DrivesLocalHud(EnumAppSide.Client, LocalPlayer.ToUpperInvariant(), LocalPlayer));
        Assert.False(LocalObservationGate.DrivesLocalHud(EnumAppSide.Client, LocalPlayer + " ", LocalPlayer));
    }

    [Fact]
    public void A_Null_Entity_Is_Not_A_Local_Interaction()
    {
        Assert.False(LocalObservationGate.IsLocalPlayerInteraction(null));
    }
}
