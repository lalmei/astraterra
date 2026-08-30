using System.Text.Json;

namespace AstraTerra.Constellations;

public static class ConstellationPersistence
{
    public const int CurrentSchemaVersion = 1;

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
        return ConstellationJournal.FromSnapshot(Migrate(snapshot));
    }

    private static JournalSnapshot Migrate(JournalSnapshot snapshot)
        => snapshot.Version switch
        {
            // Journals written before the version field was introduced have the same shape as v1.
            0 => snapshot with { Version = CurrentSchemaVersion },
            CurrentSchemaVersion => snapshot,
            _ => throw new InvalidOperationException(
                $"Unsupported constellation journal version {snapshot.Version}.")
        };
}
