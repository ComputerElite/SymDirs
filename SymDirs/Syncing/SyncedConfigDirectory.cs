using System.ComponentModel.DataAnnotations.Schema;
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
    /// Shows whether this is a source directory or a target directory.
    /// </summary>
    [JsonIgnore]
    public bool IsSourceDirectory { get; set; } = true;
    
    /// <summary>
    /// For target directories, this will have a copy of the source directory that this directory is synced with.
    /// </summary>
    private SyncedConfigDirectory? _syncedWith = null;
    
    /// <summary>
    /// Reference to the info for the directory on the local filesystem.
    /// </summary>
    [JsonIgnore]
    [NotMapped]
    public LocalDirectory? LocalDirectory { get; set; } = null;
    
    /// <summary>
    /// If the directory is located in other known directories, these will be listed here.
    /// </summary>
    [NotMapped]
    public List<SyncedConfigParentDirectory> LocatedIn { get; set; } = new();

    /// <summary>
    /// Return the root directory to use for all syncing operations (Target directories must have the _syncedWith property set).
    ///
    /// On TargetDirectories with the _syncedWith property set, this will return the path to the folder that is synced with the respective source directory.
    /// </summary>
    /// <returns></returns>
    public string? GetRootDirectoryForSyncingOperations()
    {
        if (IsSourceDirectory) return LocalDirectory?.GetPathWithTrailingSlash();
        if (LocalDirectory == null || _syncedWith == null) return null;
        if (_syncedWith.FolderName.Trim() == string.Empty)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"The source directory {_syncedWith.Id} does not have a folder name set. This is required for syncing to work properly.");
            return null;
        }
        string path = Path.Combine(LocalDirectory.Path, _syncedWith.FolderName);
        return path.EndsWith(Path.DirectorySeparatorChar) ? path : $"{path}{Path.DirectorySeparatorChar}";
    }

    /// <summary>
    /// This will always return the local directory path for indexing operations. This means it is not affected by the _syncedWith property.
    /// </summary>
    /// <returns></returns>
    public string? GetRootDirectoryForIndexingOperations()
    {
        if(LocalDirectory == null) return null;
        return LocalDirectory?.GetPathWithTrailingSlash();
    }

    /// <summary>
    /// Returns the folder marker expected in the root directory for syncing operations
    /// </summary>
    /// <returns></returns>
    public string? GetExpectedFolderMarkerForSyncingOperations()
    {
        if (LocalDirectory == null) return null;
        if(IsSourceDirectory) return Id;
        if(_syncedWith == null) return null;
        return _syncedWith.Id;
    }

    /// <summary>
    /// Checks for folder markers in the synced directory. It will only return true when the directory is supposed to be used and has the correct markers
    /// </summary>
    /// <returns></returns>
    public bool HasCorrectFolderMarkers()
    {
        string? rootDirectoryIndexing = GetRootDirectoryForIndexingOperations();
        if(rootDirectoryIndexing == null) return false;
        string? id = FolderMarker.GetIdOfDirectory(rootDirectoryIndexing);
        if(id == null) return false;
        if (id != Id) return false;
        if (!IsSourceDirectory && _syncedWith != null) // If we're linked to a source directory we also check the subdirectory we sync into
        {
            string? rootDirectorySyncing = GetRootDirectoryForIndexingOperations();
            if(rootDirectorySyncing == null) return false;
            id = FolderMarker.GetIdOfDirectory(rootDirectorySyncing);
            if(id == null) return false;
            if (id != GetExpectedFolderMarkerForSyncingOperations()) return false;
        }

        return true;
    }
    
    public void SetSyncedWithDirectory(SyncedConfigDirectory syncedWith)
    {
        _syncedWith = syncedWith;
    }

    public override string ToString()
    {
        return $"{Id}   {DisplayName} ({FolderName}) - Source: {IsSourceDirectory} - Local: {LocalDirectory?.Path ?? "Not set"}";
    }
}