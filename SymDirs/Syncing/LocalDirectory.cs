namespace SymDirs.Syncing;

public class LocalDirectory
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;

    public SyncedConfigDirectory? AsMockSyncedConfigDirectory()
    {
        return new SyncedConfigDirectory
        {
            Id = Id,
            LocalDirectory = this
        };
    }
}