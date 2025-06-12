using System.Runtime.CompilerServices;
using System.Text.Json;

namespace SymDirs.Syncing;

public class SyncedConfig : BaseConfig
{
    /// <summary>
    /// Loads the config from the specified path. If the file does not exist, it will create a new config.
    /// </summary>
    /// <param name="path"></param>
    public static SyncedConfig Load(string path)
    {
        return Load<SyncedConfig>(path);
    }

    /// <summary>
    /// Saves the config to the path it was previously loaded from.
    /// </summary>
    public void Save()
    {
        Save(this);
    }
    
    public List<SyncedConfigDirectory> SourceDirectories { get; set; } = new ();
    public List<SyncedConfigDirectory> TargetDirectories { get; set; } = new ();
}