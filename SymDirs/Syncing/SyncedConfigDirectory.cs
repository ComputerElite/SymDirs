using System.Text.Json.Serialization;

namespace SymDirs.Syncing;

public class SyncedConfigDirectory
{
    /// <summary>
    /// Id of the directory
    /// </summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// This MUST be the name of the folder as on the filesystem. NO full path!
    /// It is used to automatically update a local config once a new directory is synced with a device.
    /// </summary>
    public string FolderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the directory in the UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Whether the directory has a matching local directory configured
    /// </summary>
    public bool CanBeUsed => LocalDirectory != null;
    
    /// <summary>
    /// Shows whether this is a source directory or a target directory.
    /// </summary>
    public bool IsSourceDirectory { get; set; } = true;
    
    /// <summary>
    /// For target directories, this will have a copy of the source directory that this directory is synced with.
    /// </summary>
    private SyncedConfigDirectory? _syncedWith = null;
    
    /// <summary>
    /// Reference to the info for the directory on the local filesystem.
    /// </summary>
    [JsonIgnore]
    public LocalDirectory? LocalDirectory { get; set; } = null;
    
    /// <summary>
    /// If the directory is located in other known directories, these will be listed here.
    /// </summary>
    public List<SyncedConfigParentDirectory> LocatedIn { get; set; } = new();

    public void SetSyncedWithDirectory(SyncedConfigDirectory syncedWith)
    {
        _syncedWith = syncedWith;
    }
}