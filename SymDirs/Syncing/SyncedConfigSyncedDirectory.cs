namespace SymDirs.Syncing;

public class SyncedConfigSyncedDirectory
{
    public string SourceDirectoryId { get; set; } = string.Empty;
    public string TargetDirectoryId { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{SourceDirectoryId} --> {TargetDirectoryId}";
    }

    public string ToString(Dictionary<string, string> idToName)
    {
        return  $"{SourceDirectoryId} {idToName[SourceDirectoryId]} --> {TargetDirectoryId} {idToName[TargetDirectoryId]}";
    }
}