using AstraTerra.Astronomy;
using AstraTerra.Commands;
using AstraTerra.Observation;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra.Constellations;

public sealed class ConstellationBookServer
{
    public const string ChannelName = "astraterraconstellationbook";

    private readonly Func<StarCatalog?> catalogProvider;
    private ICoreServerAPI? api;
    private IServerNetworkChannel? channel;

    public ConstellationBookServer(Func<StarCatalog?> catalogProvider)
    {
        this.catalogProvider = catalogProvider;
    }

    public void Register(ICoreServerAPI api)
    {
        this.api = api;
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

        if (ConstellationBookMutationActions.IsPlanetAction(packet.Action))
        {
            return HandlePlanetMutation(slot, packet);
        }

        if (ConstellationBookMutationActions.IsObservationAction(packet.Action))
        {
            return HandleObservationMutation(slot, packet);
        }

        var wasConstellationBook = ConstellationBookService.IsConstellationBook(slot.Itemstack);
        var bookTitle = wasConstellationBook
            ? slot.Itemstack.Attributes.GetString(ConstellationBookService.VanillaTitleAttribute, null)
            : null;
        var journalRead = ConstellationBookService.ReadJournalResult(slot.Itemstack);
        if (journalRead.Status == ConstellationJournalReadStatus.Unreadable)
        {
            var titleForLog = string.IsNullOrWhiteSpace(bookTitle)
                ? ConstellationBookService.BookTitle
                : bookTitle;
            api?.Logger.Error(
                "AstraTerra refused to edit unreadable constellation book '{0}' "
                + "(journalJsonLength={1}): {2}",
                titleForLog,
                journalRead.JsonLength,
                journalRead.Exception);
            return Error(
                "This constellation book's contents could not be read. "
                + "The edit was refused to avoid overwriting them.");
        }

        var journal = journalRead.Journal ?? new ConstellationJournal();
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

            ConstellationBookService.WriteJournal(slot.Itemstack, journal, bookTitle);
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

    /// <summary>
    /// Names a wanderer the observer has already picked out of their own sightings.
    /// </summary>
    /// <remarks>
    /// Naming is all that is left here. A wanderer used to enter this half of the book because the
    /// game recognised one near the crosshair; now it gets in only by being classified out of the
    /// ledger, so what arrives here is a body the observer has already concluded something about,
    /// and the only thing outstanding is what to call it.
    /// </remarks>
    private static ConstellationBookResponsePacket HandlePlanetMutation(
        ItemSlot slot,
        ConstellationBookMutationPacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.PlanetId))
        {
            return Error("No planet was selected.");
        }

        if (slot.Itemstack is not { } stack)
        {
            return Error("Hold a writable or written constellation book in your left hand.");
        }

        try
        {
            var journal = ConstellationBookService.ReadPlanetJournalOrEmpty(stack);
            var record = journal.Rename(packet.PlanetId, packet.Name);

            ConstellationBookService.WritePlanetJournal(stack, journal);
            slot.MarkDirty();
            return new ConstellationBookResponsePacket
            {
                Success = true,
                Message = string.IsNullOrWhiteSpace(record.Name)
                    ? "Cleared the name of a wandering star."
                    : $"Named a wandering star {record.Name}."
            };
        }
        catch (Exception exception)
        {
            return Error($"Could not update the planet book: {exception.Message}");
        }
    }

    /// <summary>
    /// Writes a sighting into the ledger, leaving the figures and the wanderers alone.
    /// </summary>
    /// <remarks>
    /// Nothing is checked against the sky model, because there is nothing to check: the observer
    /// reports where they pointed and what the scale read, and the book's job is to keep that
    /// faithfully rather than to grade it. The angles are truncated to the instrument's resolution
    /// on the way in, so a fine reading cannot be smuggled in by a crude instrument.
    /// </remarks>
    private static ConstellationBookResponsePacket HandleObservationMutation(
        ItemSlot slot,
        ConstellationBookMutationPacket packet)
    {
        if (slot.Itemstack is not { } stack)
        {
            return Error("Hold a writable or written constellation book in your left hand.");
        }

        if (packet.Action == ConstellationBookMutationActions.ClassifySighting)
        {
            return HandleClassifySighting(slot, stack, packet);
        }

        if (!double.IsFinite(packet.AltitudeDeg) || !double.IsFinite(packet.AzimuthDeg))
        {
            return Error("That sighting has no angle to write down.");
        }

        try
        {
            var log = ConstellationBookService.ReadObservationLogOrEmpty(stack);
            var record = log.Record(
                packet.AltitudeDeg,
                packet.AzimuthDeg,
                double.IsFinite(packet.VisualMagnitude) ? packet.VisualMagnitude : null,
                packet.Day,
                packet.Hour,
                packet.LatitudeDeg,
                packet.ResolutionDeg > 0.0 ? packet.ResolutionDeg : InstrumentResolution.BrassSextantDeg,
                double.IsFinite(packet.SiderealAngleDeg) ? packet.SiderealAngleDeg : null);

            ConstellationBookService.WriteObservationLog(stack, log);
            slot.MarkDirty();
            return new ConstellationBookResponsePacket
            {
                Success = true,
                Message = $"Sighting #{record.Id} written down: {record.Describe()}."
            };
        }
        catch (Exception exception)
        {
            return Error($"Could not write the sighting down: {exception.Message}");
        }
    }

    /// <summary>
    /// Writes down what the observer concluded a set of sightings was.
    /// </summary>
    /// <remarks>
    /// The conclusion is taken at face value. Nothing here checks it against the sky, because a
    /// classification that the game has already validated is not a discovery — it is a quiz with the
    /// answer printed underneath. Being wrong is allowed, and it shows up later as an instrument
    /// that forecasts badly from the record it was given.
    /// </remarks>
    private static ConstellationBookResponsePacket HandleClassifySighting(
        ItemSlot slot,
        ItemStack stack,
        ConstellationBookMutationPacket packet)
    {
        if (packet.RecordIds.Length == 0)
        {
            return Error("Say which sightings the conclusion rests on.");
        }

        if (!Enum.IsDefined(typeof(SkyClass), packet.SkyClass) || (SkyClass)packet.SkyClass == SkyClass.Unclassified)
        {
            return Error("Say what those sightings were: a fixed star, a wanderer, or a comet.");
        }

        try
        {
            var log = ConstellationBookService.ReadObservationLogOrEmpty(stack);
            var boundId = (SkyClass)packet.SkyClass == SkyClass.Wanderer ? packet.PlanetId : null;
            var claim = log.Claim((SkyClass)packet.SkyClass, packet.Name, packet.RecordIds, packet.Day, boundId);

            ConstellationBookService.WriteObservationLog(stack, log);

            // A conclusion the observer's own entries pinned to one wandering body earns its place
            // in the half of the book the instruments read, so their name for it is the name the
            // sky answers to. Unbound conclusions still stand; they just have nothing to aim with.
            var bound = !string.IsNullOrWhiteSpace(claim.BoundId);
            if (bound)
            {
                var planets = ConstellationBookService.ReadPlanetJournalOrEmpty(stack);
                planets.Rename(claim.BoundId!, claim.Name);
                ConstellationBookService.WritePlanetJournal(stack, planets);
            }

            slot.MarkDirty();
            return new ConstellationBookResponsePacket
            {
                Success = true,
                Message = $"Written down as {SightingClaim.Describe(claim.Class).ToLowerInvariant()}: "
                          + $"{claim.DisplayName}, {claim.Provenance}."
                          + (bound ? " Your instruments call it that from now on." : string.Empty),

                // Concluding that something is a wanderer is the moment the observer has earned the
                // right to name it, so that is when the book asks. Naming stays optional: an
                // unnamed conclusion still stands, and classifying again is how it gets renamed.
                PromptNamePlanetId = bound && string.IsNullOrWhiteSpace(claim.Name)
                    ? claim.BoundId!
                    : string.Empty
            };
        }
        catch (Exception exception)
        {
            return Error($"Could not write the conclusion down: {exception.Message}");
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
            return MutationResult.Failure("A constellation segment needs two different stars.");
        }

        var createsNewConstellation = !journal.Constellations.Any(record =>
            record.Edges.Any(edge => edge.A == startHip || edge.A == endHip || edge.B == startHip || edge.B == endHip));
        var record = journal.AddEdgeAndMerge(startHip, endHip);
        return MutationResult.SuccessResult(
            $"Connected stars {startHip} and {endHip} in constellation #{record.Id}.",
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
