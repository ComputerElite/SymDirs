namespace SymDirs.Syncing;

public class LocalConfig : BaseConfig
{
    /// <summary>
    /// Loads the config from the specified path. If the file does not exist, it will create a new config.
    /// </summary>
    /// <param name="path"></param>
    public static LocalConfig Load(string path)
    {
        return Load<LocalConfig>(path);
    }

    /// <summary>
    /// Saves the config to the path it was previously loaded from.
    /// </summary>
    public void Save()
    {
        Save(this);
    }
    
    public List<LocalDirectory> Directories { get; set; } = new();

    public LocalDirectory? GetDirectoryById(string id)
    {
        return Directories.FirstOrDefault(x => x.Id == id);
    }
}