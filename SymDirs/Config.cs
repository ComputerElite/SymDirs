using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace SymDirs;

public class BaseConfig
{
    private string _path = "config.json";
    protected static T Load<T>(string path) where T : BaseConfig, new()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = factory.CreateLogger($"Config<{typeof(T).Name}>");
        if (!File.Exists(path))
        {
            // Create a new config file if it does not exist
            logger.LogInformation("Creating new config file as no file exists");
            try
            {
                File.WriteAllText(path, "{}");
            }
            catch (Exception e)
            {
                logger.LogError($"Could not create new config file. Saving will be impacted!\n{e}");
            }
        }
        T config = new T();
        try
        {
            logger.LogInformation($"Loading config from {path}");
            config = JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            logger.LogError($"Error loading config from {path}: {e}");
        }

        config._path = path;
        return config;
    }

    protected void Save<T>(T config) where T : BaseConfig
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = factory.CreateLogger($"Config<{typeof(T).Name}>");
        logger.LogInformation($"Saving config file to {_path}");
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(config));
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            logger.LogError($"Error saving config to {_path}: {e}");
        }
    }
}

public class Config : BaseConfig
{
    public List<ConfigDirectory> SourceDirectories { get; set; } = new ();
    public List<string> SourceDirectorySources { get; set; } = new ();
    public List<ConfigDirectory> TargetDirectories { get; set; } = new ();
    public bool AllowTargetToSourceFileAdding { get; set; } = true;
    public string LocalConfigPath { get; set; } = Path.Combine(GetConfigDirectory(), "localconfig." + Dns.GetHostName() + ".json");
    public string SyncedConfigPath { get; set; } = Path.Combine(GetConfigDirectory(), "syncedconfig.json");

    public static string GetConfigDirectory()
    {
        string configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") 
                           ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        string appConfigDir = Path.Combine(configDir, "sym-dirs");

        // Ensure the directory exists
        Directory.CreateDirectory(appConfigDir);
        return appConfigDir;
    }
    
    public static Config Load(string? path)
    {
        if (String.IsNullOrEmpty(path))
        {
            path = Path.Join(GetConfigDirectory(), "config.json");
        }
        Config c = Load<Config>(path);
        // apply stuff for legacy symdirs functionality
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
        Save(this);
        foreach(ConfigDirectory dir in TargetDirectories)
        {
            if (dir.Path == null) continue;
            string path = Path.GetFullPath(Path.Combine(dir.Path, "symdirs." + Dns.GetHostName() + ".config.json"));
            File.WriteAllText(path, JsonSerializer.Serialize(this));
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
    public List<string> presentFiles { get; set; } = new ();
    public Dictionary<string, List<string>> MissingContent { get; set; } = new ();

    public string? Name => System.IO.Path.GetFileName(Path);
    public string? DisplayName => Name + (Exists ? "" : " (missing)");
    [JsonIgnore]
    public int DisplayNameLength => DisplayName?.Length ?? 0;

    public bool Exists { get; set; } = true;

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