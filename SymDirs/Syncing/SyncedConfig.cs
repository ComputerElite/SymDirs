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

    public BooleanMessage<SyncedConfigDirectory> InternalAddDirectory(string path)
    {
        // Check whether the directory even exists
        if (!Directory.Exists(path))
        {
            return new BooleanMessage<SyncedConfigDirectory>("The specified directory does not exist", false, null);
        }
        
        SyncedConfigDirectory dir = new();
        dir.FolderName = Path.GetFileName(path);
        
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
        BooleanMessage<SyncedConfigDirectory> result = InternalAddDirectory(path);
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
        BooleanMessage<SyncedConfigDirectory> result = InternalAddDirectory(path);
        if (!result.Success || result.Data == null)
        {
            return new BooleanMessage("Failed to add source directory: " + result.Message, false);
        }
        result.Data.IsSourceDirectory = false;
        TargetDirectories.Add(result.Data);
        return new BooleanMessage("Source directory '" + result.Data.FolderName + "' added with ID '" + result.Data.Id + "'", true);
    }
}