using AstraTerra.Observation;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra.Constellations;

[ProtoContract]
public sealed class SkyDiscEngravePacket
{
    [ProtoMember(1)]
    public int StartHip;

    [ProtoMember(2)]
    public int EndHip;
}

[ProtoContract]
public sealed class SkyDiscEngraveResponsePacket
{
    [ProtoMember(1)]
    public bool Success;

    [ProtoMember(2)]
    public string Message = string.Empty;
}

/// <summary>
/// Cutting a line into the held disc, which only the server may do.
/// </summary>
/// <remarks>
/// A line is drawn with the mouse, and the mouse is a client. The band on the same disc is written
/// by an item interaction, which the server runs itself; a figure has no such interaction to ride
/// on, so it needs a message of its own.
/// <para>
/// Small and separate from the constellation book's channel on purpose: that channel refuses
/// everything unless a writable book is in the left hand, and a disc is engraved with neither book
/// nor ink.
/// </para>
/// </remarks>
public sealed class SkyDiscEngraveServer
{
    public const string ChannelName = "astraterraskydiscengrave";

    private IServerNetworkChannel? channel;

    public void Register(ICoreServerAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        channel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<SkyDiscEngravePacket>()
            .RegisterMessageType<SkyDiscEngraveResponsePacket>();
        channel.SetMessageHandler<SkyDiscEngravePacket>(OnEngrave);
    }

    private void OnEngrave(IServerPlayer fromPlayer, SkyDiscEngravePacket packet)
    {
        if (channel is null)
        {
            return;
        }

        channel.SendPacket(Engrave(fromPlayer, packet), fromPlayer);
    }

    private static SkyDiscEngraveResponsePacket Engrave(IServerPlayer player, SkyDiscEngravePacket packet)
    {
        var slot = SkyDiscHeld.FindSlot(player);
        if (slot?.Itemstack is not { } stack)
        {
            return new SkyDiscEngraveResponsePacket { Message = "Hold the disc you want to engrave." };
        }

        var figure = SkyDiscFigureStore.ReadOrEmpty(stack);
        var result = figure.Engrave(
            packet.StartHip,
            packet.EndHip,
            SkyDiscEngraving.FiguresAllowed(stack),
            SkyDiscEngraving.IsWorkable(stack),
            SkyDiscScribingTool.HasInHotbar(player));
        if (!result.Changed)
        {
            return new SkyDiscEngraveResponsePacket { Message = result.Message };
        }

        SkyDiscFigureStore.Write(stack, figure);

        // Without this the line exists only in the server's copy, and the sky that shows it is drawn
        // on the client.
        slot.MarkDirty();

        return new SkyDiscEngraveResponsePacket { Success = true, Message = result.Message };
    }
}

/// <summary>The client half: asks for a line, and says out loud what came back.</summary>
public sealed class SkyDiscEngraveClient
{
    private readonly ICoreClientAPI api;
    private IClientNetworkChannel? channel;

    public SkyDiscEngraveClient(ICoreClientAPI api)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public void Register()
    {
        channel = api.Network.RegisterChannel(SkyDiscEngraveServer.ChannelName)
            .RegisterMessageType<SkyDiscEngravePacket>()
            .RegisterMessageType<SkyDiscEngraveResponsePacket>();
        channel.SetMessageHandler<SkyDiscEngraveResponsePacket>(OnResponse);
    }

    public void SendEngrave(int startHip, int endHip)
        => channel?.SendPacket(new SkyDiscEngravePacket { StartHip = startHip, EndHip = endHip });

    /// <summary>
    /// The disc as the client last saw it. Drawn from the stack rather than asked of the server, so
    /// the sky can be redrawn every frame; the server's copy is the one that counts, and it arrives
    /// through the slot going dirty.
    /// </summary>
    public SkyDiscFigure? ReadHeldFigure()
        => SkyDiscHeld.Find(api.World?.Player) is { } stack ? SkyDiscFigureStore.Read(stack) : null;

    private void OnResponse(SkyDiscEngraveResponsePacket packet)
    {
        if (!string.IsNullOrWhiteSpace(packet.Message))
        {
            // On the disc's own face while it is up, so the observer is not made to look away from
            // the sky they are drawing on to read the answer.
            SkyDiscReadingState.ReportMark(packet.Message);
        }
    }
}
