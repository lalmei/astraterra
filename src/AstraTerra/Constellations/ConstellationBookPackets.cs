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
}

public static class ConstellationBookMutationActions
{
    public const string AddEdge = "addEdge";
    public const string Rename = "rename";
    public const string RemoveEdge = "removeEdge";
    public const string Delete = "delete";
    public const string Build = "build";
}
