using System.Runtime.CompilerServices;
using System.Text.Json;
using SymDirs.ReturnTypes;

namespace SymDirs.Syncing;

public class SyncedConfig : BaseConfig
{
    /// <summary>
    /// Loads the config from the specified path. If the file does not exist, it will create a new config.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="localConfig"></param>
    public static SyncedConfig Load(string path, LocalConfig localConfig)
    {
        SyncedConfig config = Load<SyncedConfig>(path);
        config._localConfig = localConfig;
        config.SourceDirectories.ForEach(x => x.IsSourceDirectory = true);
        config.TargetDirectories.ForEach(x => x.IsSourceDirectory = false);
        return config;
    }

    /// <summary>
    /// Saves the config to the path it was previously loaded from. Also saved the local config.
    /// </summary>
    public void Save()
    {
        Save(this);
        _localConfig.Save();
    }
    
    public List<SyncedConfigDirectory> SourceDirectories { get; set; } = new ();
    public List<SyncedConfigDirectory> TargetDirectories { get; set; } = new ();
    public List<SyncedConfigSyncedDirectory> SyncedDirectories { get; set; } = new ();

    private LocalConfig _localConfig = new();

    public List<SyncedConfigDirectory> GetSourceDirectories()
    {
        return SourceDirectories.Select(x =>
        {
            x.LocalDirectory = _localConfig.GetDirectoryById(x.Id);
            return x;
        }).ToList();
    }

    public SyncedConfigDirectory? GetSourceDirectoryById(string id)
    {
        SyncedConfigDirectory? dir = SourceDirectories.FirstOrDefault(x => x.Id == id);
        if (dir == null)
            return null;
        dir.LocalDirectory = _localConfig.GetDirectoryById(id);
        return dir;
    }
    public List<SyncedConfigDirectory> GetTargetDirectories()
    {
        return TargetDirectories.Select(x =>
        {
            x.LocalDirectory = _localConfig.GetDirectoryById(x.Id);
            return x;
        }).ToList();
    }
    public SyncedConfigDirectory? GetTargetDirectoryById(string id)
    {
        SyncedConfigDirectory? dir = TargetDirectories.FirstOrDefault(x => x.Id == id);
        if (dir == null)
            return null;
        dir.LocalDirectory = _localConfig.GetDirectoryById(id);
        return dir;
    }
    
    /// <summary>
    /// Gets a list of all directories that exist in the config.
    /// </summary>
    /// <returns></returns>
    public List<SyncedConfigDirectory> AllDirectories()
    {
        List<SyncedConfigDirectory> dirs = new();
        dirs.AddRange(SourceDirectories);
        dirs.AddRange(TargetDirectories);
        dirs.ForEach(x =>

            x.LocalDirectory = _localConfig.GetDirectoryById(x.Id)
        );
        return dirs;
    }
    
    /// <summary>
    /// Return all directories that are linked to the specified directory.
    /// </summary>
    /// <param name="directory">Directory you want to get all linked directories of</param>
    /// <returns>SyncedConfigDirectories populated with syncedWith</returns>
    public List<SyncedConfigDirectory> GetLinkedDirectories(SyncedConfigDirectory directory)
    {
        // This gets a list of all directories that are linked to the specified directory.
        SyncedConfigDirectory? syncedFrom = directory.IsSourceDirectory ? directory : SourceDirectories.FirstOrDefault(x => x.Id == SyncedDirectories.FirstOrDefault(x => x.TargetDirectoryId == directory.Id)?.SourceDirectoryId);
        if (syncedFrom == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No source directory for the given directory specified. This means that it's not synced. Returning empty array.");
            return [];
        }
        List<SyncedConfigDirectory> result =  SyncedDirectories.Where(x => x.SourceDirectoryId == syncedFrom.Id)
            .Select(x => TargetDirectories.FirstOrDefault(y => y.Id == x.TargetDirectoryId))
            .Where(x => x != null)
            .Select(x => x!)
            .ToList();
        result.Add(syncedFrom);
        result.ForEach(x =>
            x.LocalDirectory = _localConfig.GetDirectoryById(x.Id)
        );
        result.ForEach(x =>
        {
            if (x.IsSourceDirectory) return;
            x.SetSyncedWithDirectory(syncedFrom);
        });
        return result;
    }

    private BooleanMessage<SyncedConfigDirectory> _internalAddDirectory(string path)
    {
        // Check whether the directory even exists
        if (!Directory.Exists(path))
        {
            return new BooleanMessage<SyncedConfigDirectory>("The specified directory does not exist", false, null);
        }
        
        SyncedConfigDirectory dir = new();
        dir.FolderName = Path.GetFileName(path);
        dir.DisplayName = dir.FolderName;
        
        // If it does exist, check whether it has a folder marker
        string? directoryId = FolderMarker.GetIdOfDirectory(path);
        if (directoryId != null)
        {
            // Directory was previously tracked, check whether it's already in the config
            SyncedConfigDirectory? existing = SourceDirectories.FirstOrDefault(x => x.Id == directoryId) ??
                                             TargetDirectories.FirstOrDefault(x => x.Id == directoryId) ??
                                             _localConfig.GetDirectoryById(directoryId)?.AsMockSyncedConfigDirectory();
            if (existing != null)
            {
                existing.LocalDirectory = _localConfig.GetDirectoryById(directoryId);
                return new BooleanMessage<SyncedConfigDirectory>("A directory with the ID '" + directoryId + "' is already tracked at '" + existing.LocalDirectory?.Path + "'", false, null);
            }
            
            // The directory isn't tracked but already has a Id, therefore we can just use it
            dir.Id = directoryId;
        }
        else
        {
            // We need to generate a new Id for the directory and create a folder marker
            dir.Id = Guid.NewGuid().ToString();
            BooleanMessage folderMarkerResult = FolderMarker.CreateDirectoryMarker(path, dir.Id);
            Console.WriteLine(folderMarkerResult.Message);
            if (!folderMarkerResult.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(folderMarkerResult.Message);
                return folderMarkerResult.WithData<SyncedConfigDirectory>(null);
            }
        }
        LocalDirectory localDirectory = new();
        localDirectory.Id = dir.Id;
        localDirectory.Path = path;
        _localConfig.Directories.Add(localDirectory);
        dir.LocalDirectory = _localConfig.GetDirectoryById(dir.Id);
        return new BooleanMessage<SyncedConfigDirectory>("Directory '" + dir.FolderName + "' added with ID '" + dir.Id + "'", true, dir);
    }

    /// <summary>
    /// Adds a new Source directory to the config.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public BooleanMessage AddSourceDirectory(string path)
    {
        BooleanMessage<SyncedConfigDirectory> result = _internalAddDirectory(path);
        if (!result.Success || result.Data == null)
        {
            return new BooleanMessage("Failed to add source directory: " + result.Message, false);
        }
        result.Data.IsSourceDirectory = true;
        SourceDirectories.Add(result.Data);
        return new BooleanMessage("Source directory '" + result.Data.FolderName + "' added with ID '" + result.Data.Id + "'", true);
    }
    
    /// <summary>
    /// Adds a new Target directory to the config.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public BooleanMessage AddTargetDirectory(string path)
    {
        BooleanMessage<SyncedConfigDirectory> result = _internalAddDirectory(path);
        if (!result.Success || result.Data == null)
        {
            return new BooleanMessage("Failed to add source directory: " + result.Message, false);
        }
        result.Data.IsSourceDirectory = false;
        TargetDirectories.Add(result.Data);
        return new BooleanMessage("Source directory '" + result.Data.FolderName + "' added with ID '" + result.Data.Id + "'", true);
    }

    public BooleanMessage CreateLink(string sourceDirectoryId, string targetDirectoryId)
    {
        SyncedConfigDirectory? sourceDir = GetSourceDirectoryById(sourceDirectoryId);
        if (sourceDir == null)
            return new BooleanMessage($"A source directory with the Id '{sourceDirectoryId}' does not exist", false);
        SyncedConfigDirectory? targetDir = GetTargetDirectoryById(targetDirectoryId);
        if (targetDir == null)
            return new BooleanMessage($"A target directory with the Id '{targetDirectoryId}' does not exist", false);
        targetDir.SetSyncedWithDirectory(sourceDir);
        // Check whether the target dir root (subdir of the target dir) has the id of the source dir.
        string? targetRootPath = targetDir.GetRootDirectoryForSyncingOperations();
        if (targetRootPath == null)
        {
            return new BooleanMessage("Target directory could not resolve a root directory, that's unusual ;-;", false);
        }

        if (!Directory.Exists(targetRootPath))
        {
            Directory.CreateDirectory(targetRootPath);
        }

        string? detectedTargetDirId = FolderMarker.GetIdOfDirectory(targetRootPath);
        if (detectedTargetDirId != null && detectedTargetDirId != sourceDirectoryId)
        {
            return new BooleanMessage(
                $"The target directory '{targetRootPath}' already has a folder marker with the ID '{detectedTargetDirId}', which does not match the source directory ID '{sourceDirectoryId}'. Therefore the link will not be created.",
                false);
        }

        BooleanMessage folderMarkerCreatedMessage =
            FolderMarker.CreateDirectoryMarker(targetRootPath, sourceDirectoryId);
        if (!folderMarkerCreatedMessage.Success)
        {
            return new BooleanMessage(
                $"Failed to create folder marker for target directory '{targetRootPath}': {folderMarkerCreatedMessage.Message}",
                false);
        }

        SyncedConfigSyncedDirectory syncedDirectory = new()
        {
            SourceDirectoryId = sourceDir.Id,
            TargetDirectoryId = targetDir.Id
        };
        SyncedDirectories.Add(syncedDirectory);
        return new BooleanMessage($"Link created between source directory '{sourceDir.Id}' ({sourceDir.GetRootDirectoryForSyncingOperations()}) and target directory '{targetDir.FolderName}' ({targetRootPath})", true);
    }

    public BooleanMessage RemoveLink(string sourceDirectoryId, string targetDirectoryId)
    {
        // This will just unconditionally remove a link, the SyncController will handle everything else in regards to unlinking
        SyncedConfigSyncedDirectory? syncedDirectory = SyncedDirectories.FirstOrDefault(x => x.SourceDirectoryId == sourceDirectoryId && x.TargetDirectoryId == targetDirectoryId);
        if (syncedDirectory == null)
            return new BooleanMessage($"There exists no link between '{sourceDirectoryId}' and '{targetDirectoryId}'", false);
        SyncedDirectories.Remove(syncedDirectory);
        return new BooleanMessage($"Link between '{sourceDirectoryId}' and '{targetDirectoryId}' successfully removed", true);
    }
}