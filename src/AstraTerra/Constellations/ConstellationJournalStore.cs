namespace AstraTerra.Constellations;

public sealed class ConstellationJournalStore
{
    private readonly string rootDirectory;

    public ConstellationJournalStore(string rootDirectory)
    {
        this.rootDirectory = rootDirectory;
    }

    public ConstellationJournal Load(string worldIdentifier)
    {
        var path = ResolvePath(worldIdentifier);
        if (!File.Exists(path))
        {
            return new ConstellationJournal();
        }

        return ConstellationPersistence.Deserialize(File.ReadAllText(path));
    }

    public ConstellationJournal LoadUnmigrated(string worldIdentifier)
    {
        return IsMigrated(worldIdentifier) ? new ConstellationJournal() : Load(worldIdentifier);
    }

    public void Save(string worldIdentifier, ConstellationJournal journal)
    {
        var path = ResolvePath(worldIdentifier);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConstellationPersistence.Serialize(journal));
    }

    public bool IsMigrated(string worldIdentifier)
        => File.Exists(ResolveMigratedMarkerPath(worldIdentifier));

    public void MarkMigrated(string worldIdentifier)
    {
        var path = ResolvePath(worldIdentifier);
        var backupPath = ResolveMigratedBackupPath(worldIdentifier);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path) && !File.Exists(backupPath))
        {
            File.Copy(path, backupPath);
        }

        File.WriteAllText(ResolveMigratedMarkerPath(worldIdentifier), DateTimeOffset.UtcNow.ToString("O"));
    }

    public string ResolvePath(string worldIdentifier)
    {
        return Path.Combine(rootDirectory, $"{WorldStorageKeyResolver.Resolve(worldIdentifier)}.constellations.v1.json");
    }

    public string ResolveMigratedBackupPath(string worldIdentifier)
    {
        return Path.Combine(rootDirectory, $"{WorldStorageKeyResolver.Resolve(worldIdentifier)}.constellations.v1.migrated-backup.json");
    }

    public string ResolveMigratedMarkerPath(string worldIdentifier)
    {
        return Path.Combine(rootDirectory, $"{WorldStorageKeyResolver.Resolve(worldIdentifier)}.constellations.v1.migrated");
    }
}
