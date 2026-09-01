using ProtoBuf;

namespace AstraTerra.Constellations;

[ProtoContract]
public sealed class ConstellationBookMutationPacket
{
    [ProtoMember(1)]
    public string Action = string.Empty;

    [ProtoMember(2)]
    public int ConstellationId;

    [ProtoMember(3)]
    public int StartHip;

    [ProtoMember(4)]
    public int EndHip;

    [ProtoMember(5)]
    public string Name = string.Empty;

    [ProtoMember(6)]
    public string Target = string.Empty;

    [ProtoMember(7)]
    public string LegacyJournalJson = string.Empty;

    /// <summary>Catalog id of the planet being identified or named. Empty for constellation actions.</summary>
    [ProtoMember(8)]
    public string PlanetId = string.Empty;

    /// <summary>
    /// The sighting itself, for <see cref="ConstellationBookMutationActions.RecordObservation"/>.
    /// Deliberately carries no identity: the observer measured a direction, not an object.
    /// </summary>
    [ProtoMember(9)]
    public double AltitudeDeg;

    [ProtoMember(10)]
    public double AzimuthDeg;

    /// <summary>How bright it looked, or NaN when there was nothing to judge it against.</summary>
    [ProtoMember(11)]
    public double VisualMagnitude = double.NaN;

    [ProtoMember(12)]
    public int Day;

    [ProtoMember(13)]
    public double Hour;

    [ProtoMember(14)]
    public double LatitudeDeg;

    /// <summary>What the sighting instrument reads to. See <see cref="Observation.InstrumentResolution"/>.</summary>
    [ProtoMember(15)]
    public double ResolutionDeg;

    /// <summary>How far the sky had turned, so the entry can be placed among the fixed stars later.</summary>
    [ProtoMember(16)]
    public double SiderealAngleDeg = double.NaN;

    /// <summary>The entries a conclusion rests on, for <see cref="ConstellationBookMutationActions.ClassifySighting"/>.</summary>
    [ProtoMember(17)]
    public long[] RecordIds = [];

    /// <summary>What the observer says those entries were. See <see cref="Observation.SkyClass"/>.</summary>
    [ProtoMember(18)]
    public int SkyClass;

    /// <summary>Observer longitude when the sighting was written, or NaN on older clients.</summary>
    [ProtoMember(19)]
    public double LongitudeDeg = double.NaN;
}

[ProtoContract]
public sealed class ConstellationBookResponsePacket
{
    [ProtoMember(1)]
    public bool Success;

    [ProtoMember(2)]
    public string Message = string.Empty;

    [ProtoMember(3)]
    public int PromptRenameConstellationId;

    [ProtoMember(4)]
    public bool LegacyMigrationAccepted;

    /// <summary>Set when the server has just written a planet down and the observer should name it.</summary>
    [ProtoMember(5)]
    public string PromptNamePlanetId = string.Empty;
}

public static class ConstellationBookMutationActions
{
    public const string AddEdge = "addEdge";
    public const string Rename = "rename";
    public const string RemoveEdge = "removeEdge";
    public const string Delete = "delete";
    public const string Build = "build";
    public const string RenamePlanet = "renamePlanet";
    public const string RecordObservation = "recordObservation";
    public const string ClassifySighting = "classifySighting";

    /// <summary>Actions that write the planet half of the book rather than the constellation half.</summary>
    /// <remarks>
    /// Naming only. A wanderer enters this half of the book by being classified out of the
    /// observer's own sightings; there is no longer an action that writes an identity down because
    /// the game recognised something.
    /// </remarks>
    public static bool IsPlanetAction(string action)
        => action is RenamePlanet;

    /// <summary>Actions that write the ledger of sightings rather than either journal.</summary>
    public static bool IsObservationAction(string action)
        => action is RecordObservation or ClassifySighting;
}
