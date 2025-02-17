using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SymDirs;

public class Config
{
    public List<ConfigDirectory> SourceDirectories { get; set; } = new List<ConfigDirectory>();
    public List<string> SourceDirectorySources { get; set; } = new List<string>();
    public List<ConfigDirectory> TargetDirectories { get; set; } = new List<ConfigDirectory>();

    public static string GetConfigDirectory()
    {
        string configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") 
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        string appConfigDir = Path.Combine(configDir, "sym-dirs");

        // Ensure the directory exists
        Directory.CreateDirectory(appConfigDir);
        return appConfigDir;
    }
    
    public static Config Load(string path = "")
    {
        if (path == "")
        {
            path = Path.Combine(GetConfigDirectory(), "config.json");
        }
        if (!File.Exists(path))
        {
            return new Config();
        }

        Config c = JsonSerializer.Deserialize<Config>(File.ReadAllText(path)) ?? new Config();
        c.RemoveDuplicates();
        c.UpdateRelations();
        StateCreator.CheckState(c);
        return c;
    }

    private void RemoveDuplicates()
    {
        foreach (ConfigDirectory sourceDirectory in SourceDirectories)
        {
            sourceDirectory.LinkedBy = sourceDirectory.LinkedBy.Distinct().ToList();
        }
        
    }

    /// <summary>
    /// Updates all source directories to show by whom they are linked.
    /// </summary>
    public void UpdateRelations()
    {
        foreach (ConfigDirectory sourceDirectory in SourceDirectories)
        {
            if (sourceDirectory.Path == null) continue;
            sourceDirectory.Links.Clear();
            foreach (ConfigDirectory targetDirectory in TargetDirectories)
            {
                if (targetDirectory.Path == null) continue;
                if (sourceDirectory.LinkedBy.Contains(targetDirectory.Path))
                {
                    sourceDirectory.Links.Add(targetDirectory);
                }
            }
        }
        foreach (ConfigDirectory dir in TargetDirectories)
        {
            dir.LinkedBy = dir.Links.Select(x => x.Path).ToList();
        }
        foreach (ConfigDirectory dir in SourceDirectories)
        {
            dir.LinkedBy = dir.Links.Select(x => x.Path).ToList();
        }
    }
    
    public void Save()
    {
        RemoveDuplicates();
        UpdateRelations();
        string json = JsonSerializer.Serialize(this);
        File.WriteAllText(Path.Combine(GetConfigDirectory(), "config.json"), json);
        foreach(ConfigDirectory dir in TargetDirectories)
        {
            if (dir.Path == null) continue;
            Console.WriteLine(dir.Path);
            Console.WriteLine(Path.IsPathRooted(dir.Path));
            string path = Path.GetFullPath(Path.Combine(dir.Path, "symdirs.config.json"));
            Console.WriteLine(path);
            File.WriteAllText(path, json);
        }
    }
    public int AddTarget(string path)
    {
        // check if path is already added
        if (TargetDirectories.Any(x => x.Path == path)) return 0;
        TargetDirectories.Add(new ConfigDirectory { Path = path });
        return 1;
    }

    public int AddSource(string path)
    {
        // check if path is already added
        if (SourceDirectories.Any(x => x.Path == path)) return 0;
        SourceDirectories.Add(new ConfigDirectory { Path = path });
        return 1;
    }
}

public class ConfigDirectory
{
    public string? Path { get; set; } = null;
    public List<string?> LinkedBy { get; set; } = new ();
    public List<ConfigDirectory> Links = new ();
    public Dictionary<string, List<string>> MissingContent { get; set; } = new ();

    public string? Name => System.IO.Path.GetFileName(Path);
    [JsonIgnore]
    public int NameLength => Name?.Length ?? 0;

    public void Remove(ConfigDirectory targetDir)
    {
        Links.Remove(targetDir);
        LinkedBy.Remove(targetDir.Path);
    }

    public void Add(ConfigDirectory targetDir)
    {
        Links.Add(targetDir);
        LinkedBy.Add(targetDir.Path);
    }
}