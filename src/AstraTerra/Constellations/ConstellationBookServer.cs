using AstraTerra.Astronomy;
using AstraTerra.Commands;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra.Constellations;

public sealed class ConstellationBookServer
{
    public const string ChannelName = "astraterraconstellationbook";

    private readonly Func<StarCatalog?> catalogProvider;
    private IServerNetworkChannel? channel;

    public ConstellationBookServer(Func<StarCatalog?> catalogProvider)
    {
        this.catalogProvider = catalogProvider;
    }

    public void Register(ICoreServerAPI api)
    {
        channel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<ConstellationBookMutationPacket>()
            .RegisterMessageType<ConstellationBookResponsePacket>();
        channel.SetMessageHandler<ConstellationBookMutationPacket>(OnMutation);
    }

    private void OnMutation(IServerPlayer fromPlayer, ConstellationBookMutationPacket packet)
    {
        if (channel is null)
        {
            return;
        }

        var response = HandleMutation(fromPlayer, packet);
        channel.SendPacket(response, fromPlayer);
    }

    private ConstellationBookResponsePacket HandleMutation(IServerPlayer player, ConstellationBookMutationPacket packet)
    {
        var slot = ConstellationBookService.GetLeftHandBookSlot(player);
        if (slot?.Itemstack is null)
        {
            return Error("Hold a writable or written constellation book in your left hand.");
        }

        if (!ConstellationBookService.PlayerHasInkAndQuill(player))
        {
            return Error("Writing constellations requires ink and quill in your inventory.");
        }

        var wasConstellationBook = ConstellationBookService.IsConstellationBook(slot.Itemstack);
        var journal = ConstellationBookService.ReadJournalOrEmpty(slot.Itemstack);
        var legacyMigrationAccepted = false;

        if (!wasConstellationBook && TryReadLegacyJournal(packet.LegacyJournalJson, out var legacyJournal))
        {
            journal = legacyJournal;
            legacyMigrationAccepted = true;
        }

        try
        {
            var result = ApplyMutation(journal, packet);
            if (!result.Success)
            {
                return Error(result.Message);
            }

            ConstellationBookService.WriteJournal(slot.Itemstack, journal);
            slot.MarkDirty();
            return new ConstellationBookResponsePacket
            {
                Success = true,
                Message = result.Message,
                PromptRenameConstellationId = result.PromptRenameConstellationId,
                LegacyMigrationAccepted = legacyMigrationAccepted
            };
        }
        catch (Exception exception)
        {
            return Error($"Could not update constellation book: {exception.Message}");
        }
    }

    private MutationResult ApplyMutation(ConstellationJournal journal, ConstellationBookMutationPacket packet)
    {
        switch (packet.Action)
        {
            case ConstellationBookMutationActions.AddEdge:
                return AddEdge(journal, packet.StartHip, packet.EndHip);
            case ConstellationBookMutationActions.Rename:
                return Rename(journal, packet.ConstellationId, packet.Name);
            case ConstellationBookMutationActions.RemoveEdge:
                return RemoveEdge(journal, packet.ConstellationId, packet.StartHip, packet.EndHip);
            case ConstellationBookMutationActions.Delete:
                return Delete(journal, packet.ConstellationId);
            case ConstellationBookMutationActions.Build:
                return Build(journal, packet.Target);
            default:
                return MutationResult.Failure("Unknown constellation book action.");
        }
    }

    private static MutationResult AddEdge(ConstellationJournal journal, int startHip, int endHip)
    {
        if (startHip == endHip)
        {
            return MutationResult.Failure("A constellation segment needs two different guide stars.");
        }

        var createsNewConstellation = !journal.Constellations.Any(record =>
            record.Edges.Any(edge => edge.A == startHip || edge.A == endHip || edge.B == startHip || edge.B == endHip));
        var record = journal.AddEdgeAndMerge(startHip, endHip);
        return MutationResult.SuccessResult(
            $"Connected guide stars {startHip} and {endHip} in constellation #{record.Id}.",
            createsNewConstellation ? record.Id : 0);
    }

    private static MutationResult Rename(ConstellationJournal journal, int constellationId, string name)
    {
        var record = journal.Constellations.FirstOrDefault(record => record.Id == constellationId);
        if (record is null)
        {
            return MutationResult.Failure($"Constellation not found: {constellationId}");
        }

        journal.Replace(record with { Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim() });
        return MutationResult.SuccessResult($"Named constellation #{record.Id}.");
    }

    private static MutationResult RemoveEdge(ConstellationJournal journal, int constellationId, int startHip, int endHip)
    {
        var record = journal.Constellations.FirstOrDefault(record => record.Id == constellationId);
        if (record is null)
        {
            return MutationResult.Failure($"Constellation not found: {constellationId}");
        }

        if (!record.Edges.Any(edge => edge.Matches(startHip, endHip)))
        {
            return MutationResult.Failure("Constellation segment not found.");
        }

        journal.RemoveEdgeAndSplit(constellationId, startHip, endHip);
        return MutationResult.SuccessResult($"Removed segment from constellation #{constellationId}.");
    }

    private static MutationResult Delete(ConstellationJournal journal, int constellationId)
    {
        if (!journal.Remove(constellationId))
        {
            return MutationResult.Failure($"Constellation not found: {constellationId}");
        }

        return MutationResult.SuccessResult($"Deleted constellation #{constellationId}.");
    }

    private MutationResult Build(ConstellationJournal journal, string target)
    {
        var service = new StarsCommandService(journal, catalog: catalogProvider());
        var output = service.Build(target);
        return output.StartsWith("Built ", StringComparison.Ordinal)
            ? MutationResult.SuccessResult(output)
            : MutationResult.Failure(output);
    }

    private static bool TryReadLegacyJournal(string legacyJournalJson, out ConstellationJournal journal)
    {
        journal = new ConstellationJournal();
        if (string.IsNullOrWhiteSpace(legacyJournalJson))
        {
            return false;
        }

        try
        {
            var candidate = ConstellationPersistence.Deserialize(legacyJournalJson);
            if (candidate.Constellations.Count == 0)
            {
                return false;
            }

            journal = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ConstellationBookResponsePacket Error(string message)
        => new()
        {
            Success = false,
            Message = message
        };

    private readonly record struct MutationResult(bool Success, string Message, int PromptRenameConstellationId)
    {
        public static MutationResult SuccessResult(string message, int promptRenameConstellationId = 0)
            => new(true, message, promptRenameConstellationId);

        public static MutationResult Failure(string message)
            => new(false, message, 0);
    }
}
