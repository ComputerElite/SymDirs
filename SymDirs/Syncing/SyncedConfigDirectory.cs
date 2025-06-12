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
    /// Reference to the info for the directory on the local filesystem.
    /// </summary>
    [JsonIgnore]
    public LocalDirectory LocalDirectory { get; set; } = new();
    
    /// <summary>
    /// If the directory is located in other known directories, these will be listed here.
    /// </summary>
    public List<SyncedConfigParentDirectory> LocatedIn { get; set; } = new();
}