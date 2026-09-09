using AstraTerra.Astronomy.Patches;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra.Astronomy;

/// <summary>The server's answer to whether a parent giant is allowed to light its moon.</summary>
[ProtoContract]
public sealed class NearBodyLightConfigPacket
{
    [ProtoMember(1)]
    public bool Enabled;
}

/// <summary>
/// Owns one side's <see cref="NearBodyLightController"/>, the Harmony patch that feeds it into the
/// game's light, and the server policy that decides whether either is doing anything.
/// </summary>
/// <remarks>
/// <para>
/// The server's setting is the one that counts, and it is sent to every client, because this
/// setting changes what spawns and what grows. A client that lit its own nights while the server
/// it is on did not would be looking at a landscape whose rules disagree with what it looks like --
/// mobs standing in what the player sees as light. So the client waits to be told, and until it is
/// told, it does nothing.
/// </para>
/// <para>
/// This is the same shape as the longitude-aware sun's installer next door, and for the same
/// reason: both are world-affecting settings whose two sides have to hold the same value.
/// </para>
/// </remarks>
public sealed class NearBodyLightInstaller : IDisposable
{
    public const string ChannelName = "astraterranearbodylight";

    private readonly ICoreAPI api;
    private readonly NearBodyLightController controller = new();
    private readonly NearBodyLightPatchHost patch = new();
    private ICoreServerAPI? serverApi;
    private IServerNetworkChannel? serverChannel;
    private bool serverEnabled;
    private bool disposed;

    private NearBodyLightInstaller(ICoreAPI api, System.Func<double, double>? observerLongitude)
    {
        this.api = api;
        controller.Bind(api.World, observerLongitude);
    }

    /// <summary>The controller this side's light is read from.</summary>
    public NearBodyLightController Controller => controller;

    public static NearBodyLightInstaller StartClient(ICoreClientAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        // The client already has an answer for longitude, kept by the longitude-aware sun's own
        // installer, so it uses that rather than a second copy that could disagree with it.
        var installer = new NearBodyLightInstaller(api, observerLongitude: null);

        // Off until the server says otherwise: an unconfigured client should look like vanilla,
        // not like a guess about somebody else's world.
        installer.controller.SetEnabled(false);
        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<NearBodyLightConfigPacket>()
            .SetMessageHandler<NearBodyLightConfigPacket>(installer.OnServerConfig);
        installer.patch.Start(api, installer.controller);
        api.Logger.Event("AstraTerra startup step: near-body lighting awaiting server configuration");
        return installer;
    }

    /// <param name="sunFollowsLongitude">
    /// Whether this server's sun is currently being shifted by longitude. The giant has to be
    /// placed the same way the sun is, or the server would spawn by the light of a giant standing
    /// somewhere its own clients cannot see it.
    /// </param>
    public static NearBodyLightInstaller StartServer(
        ICoreServerAPI api,
        bool enabled,
        System.Func<bool> sunFollowsLongitude)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(sunFollowsLongitude);

        var installer = new NearBodyLightInstaller(
            api,
            x => sunFollowsLongitude() ? LatitudeMapper.MapWorldLongitude(x, api.World) : 0.0)
        {
            serverApi = api,
            serverEnabled = enabled
        };

        installer.controller.SetEnabled(enabled);
        installer.serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<NearBodyLightConfigPacket>();
        api.Event.PlayerNowPlaying += installer.OnPlayerNowPlaying;
        installer.patch.Start(api, installer.controller);
        api.Logger.Event(
            "AstraTerra startup step: near-body lighting server policy: enabled={0}; patched={1}",
            enabled,
            NearBodyLightPatchHost.IsAvailable);
        return installer;
    }

    /// <summary>
    /// Hands this side the giant that lights the world, or null for a world that has none.
    /// </summary>
    public void SetSource(NearBodyLightSource? source)
    {
        if (!disposed)
        {
            controller.SetSource(source);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (serverApi is not null)
        {
            serverApi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
        }

        patch.Stop();
        controller.Reset();
        disposed = true;
    }

    private void OnServerConfig(NearBodyLightConfigPacket packet)
    {
        if (disposed)
        {
            return;
        }

        controller.SetEnabled(packet.Enabled);
        api.Logger.Event(
            "AstraTerra startup step: near-body lighting server policy received: enabled={0}",
            packet.Enabled);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
        => serverChannel?.SendPacket(new NearBodyLightConfigPacket { Enabled = serverEnabled }, player);
}
