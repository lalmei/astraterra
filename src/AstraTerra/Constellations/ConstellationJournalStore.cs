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

    public void Save(string worldIdentifier, ConstellationJournal journal)
    {
        var path = ResolvePath(worldIdentifier);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, ConstellationPersistence.Serialize(journal));
    }

    public string ResolvePath(string worldIdentifier)
    {
        return Path.Combine(rootDirectory, $"{WorldStorageKeyResolver.Resolve(worldIdentifier)}.constellations.v1.json");
    }
}
