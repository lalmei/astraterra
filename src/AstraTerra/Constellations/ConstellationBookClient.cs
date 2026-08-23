using AstraTerra.Client.Hud;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Constellations;

public sealed class ConstellationBookClient
{
    private readonly ICoreClientAPI api;
    private readonly ConstellationJournal legacyJournal;
    private readonly Action onLegacyMigrated;
    private IClientNetworkChannel? channel;
    private bool legacyMigrationSent;
    private bool legacyMigrated;

    public ConstellationBookClient(ICoreClientAPI api, ConstellationJournal legacyJournal, Action onLegacyMigrated)
    {
        this.api = api;
        this.legacyJournal = legacyJournal;
        this.onLegacyMigrated = onLegacyMigrated;
        legacyMigrated = legacyJournal.Constellations.Count == 0;
    }

    public void Register()
    {
        channel = api.Network.RegisterChannel(ConstellationBookServer.ChannelName)
            .RegisterMessageType<ConstellationBookMutationPacket>()
            .RegisterMessageType<ConstellationBookResponsePacket>();
        channel.SetMessageHandler<ConstellationBookResponsePacket>(OnResponse);
    }

    public ConstellationJournal? ReadCurrentJournal()
    {
        var stack = api.World.Player.Entity.LeftHandItemSlot?.Itemstack;
        return ConstellationBookService.ReadJournal(stack);
    }

    public ConstellationJournal ReadCurrentJournalOrEmpty()
        => ReadCurrentJournal() ?? new ConstellationJournal();

    public bool HasLeftHandJournalBook()
    {
        var stack = api.World.Player.Entity.LeftHandItemSlot?.Itemstack;
        return ConstellationBookService.IsValidJournalBook(stack);
    }

    public bool CanMutate(out string message)
    {
        var player = api.World.Player;
        if (!ConstellationBookService.IsValidJournalBook(player.Entity.LeftHandItemSlot?.Itemstack))
        {
            message = "Hold a writable or written constellation book in your left hand.";
            return false;
        }

        if (!ConstellationBookService.PlayerHasInkAndQuill(player))
        {
            message = "Writing constellations requires ink and quill in your inventory.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    public void SendAddEdge(int startHip, int endHip)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.AddEdge,
            StartHip = startHip,
            EndHip = endHip
        });
    }

    public void SendRename(int constellationId, string? name)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.Rename,
            ConstellationId = constellationId,
            Name = name ?? string.Empty
        });
    }

    public void SendRemoveEdge(int constellationId, int startHip, int endHip)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.RemoveEdge,
            ConstellationId = constellationId,
            StartHip = startHip,
            EndHip = endHip
        });
    }

    public void SendDelete(int constellationId)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.Delete,
            ConstellationId = constellationId
        });
    }

    public void SendIdentifyPlanet(string planetId)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.IdentifyPlanet,
            PlanetId = planetId
        });
    }

    public void SendRenamePlanet(string planetId, string? name)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.RenamePlanet,
            PlanetId = planetId,
            Name = name ?? string.Empty
        });
    }

    /// <summary>
    /// Sends a sighting to be written down. The body's identity is deliberately not sent: what the
    /// observer measured is a direction and a moment, and what stood there is theirs to work out.
    /// </summary>
    public void SendRecordObservation(
        double altitudeDeg,
        double azimuthDeg,
        double? visualMagnitude,
        int day,
        double hour,
        double latitudeDeg,
        double resolutionDeg)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.RecordObservation,
            AltitudeDeg = altitudeDeg,
            AzimuthDeg = azimuthDeg,
            VisualMagnitude = visualMagnitude ?? double.NaN,
            Day = day,
            Hour = hour,
            LatitudeDeg = latitudeDeg,
            ResolutionDeg = resolutionDeg
        });
    }

    public void SendBuild(string target)
    {
        SendMutation(new ConstellationBookMutationPacket
        {
            Action = ConstellationBookMutationActions.Build,
            Target = target
        });
    }

    private void SendMutation(ConstellationBookMutationPacket packet)
    {
        if (!CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        if (channel?.Connected != true)
        {
            api.ShowChatMessage("Constellation book network is not connected.");
            return;
        }

        if (!legacyMigrated && !legacyMigrationSent && legacyJournal.Constellations.Count > 0)
        {
            packet.LegacyJournalJson = ConstellationPersistence.Serialize(legacyJournal);
            legacyMigrationSent = true;
        }

        channel.SendPacket(packet);
    }

    private void OnResponse(ConstellationBookResponsePacket packet)
    {
        if (!string.IsNullOrWhiteSpace(packet.Message))
        {
            api.ShowChatMessage(packet.Message);
        }

        if (packet.LegacyMigrationAccepted)
        {
            legacyMigrated = true;
            try
            {
                onLegacyMigrated();
            }
            catch (Exception exception)
            {
                api.Logger.Warning("AstraTerra could not mark legacy constellation journal as migrated: {0}", exception.Message);
            }
        }

        if (packet.PromptRenameConstellationId > 0)
        {
            var record = new ConstellationRecord(packet.PromptRenameConstellationId, null, 0, 0, []);
            new ConstellationNameDialog(api, record, SendRename).TryOpen();
        }

        if (!string.IsNullOrWhiteSpace(packet.PromptNamePlanetId))
        {
            var existing = ConstellationBookService
                .ReadPlanetJournalOrEmpty(api.World.Player.Entity.LeftHandItemSlot?.Itemstack)
                .Find(packet.PromptNamePlanetId);
            new PlanetNameDialog(api, packet.PromptNamePlanetId, existing?.Name, SendRenamePlanet).TryOpen();
        }

        if (!packet.Success)
        {
            legacyMigrationSent = legacyMigrated;
        }
    }
}
