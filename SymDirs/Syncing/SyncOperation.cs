using SymDirs.Db;

namespace SymDirs.Syncing;

public class SyncOperation
{
    public string SourcePath { get; set; }
    public string TargetPath { get; set; }
    public SyncOperationType Type { get; set; }
    public List<DbFile> AffectedFiles = new List<DbFile>();

    public override string ToString()
    {
        return $"{Type} {SourcePath} -> {TargetPath}";
    }
}

public enum SyncOperationType
{
    CreateLink,
    Delete,
    Conflict,
    Unchanged
}