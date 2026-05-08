using System.Text.Json;

namespace AstraTerra.Constellations;

public static class ConstellationPersistence
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Serialize(ConstellationJournal journal)
        => JsonSerializer.Serialize(journal.ToSnapshot(), SerializerOptions);

    public static ConstellationJournal Deserialize(string json)
    {
        var snapshot = JsonSerializer.Deserialize<JournalSnapshot>(json, SerializerOptions)
                       ?? throw new InvalidOperationException("Constellation journal JSON was empty.");
        return ConstellationJournal.FromSnapshot(snapshot);
    }
}
