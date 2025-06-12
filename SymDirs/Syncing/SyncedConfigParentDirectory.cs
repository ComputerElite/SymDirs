namespace SymDirs.Syncing;

public class SyncedConfigParentDirectory
{
    /// <summary>
    /// Id of the parent directory.
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the child when in the parent directory
    /// </summary>
    public string childPath { get; set; } = string.Empty;
}