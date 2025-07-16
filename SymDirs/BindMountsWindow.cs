using SymDirs.Index;
using SymDirs.ReturnTypes;
using SymDirs.Syncing;

namespace SymDirs;

public class BindMountsWindow
{
    private ConfigContext _configs;
    private Config _config
    {
        get => _configs.Config;
    }
    private SyncedConfig _syncedConfig
    {
        get => _configs.SyncedConfig;
        set => _configs.SyncedConfig = value;
    }
    private LocalConfig _localConfig
    {
        get => _configs.LocalConfig;
        set => _configs.LocalConfig = value;
    }
    public BindMountsWindow(ConfigContext configContext)
    {
        this._configs = configContext;
    }

    private static Dictionary<string, string> availableActions = new()
    {
        {"0", "Set shared config path"},
        {"1", "Set local config path"},
        {"5", "Add directory"},
        {"6", "Update link"},
        {"7", "Update bind mounts"},
        {"8", "Fix folder markers"},
        {"9", "Legacy Menu"},
    };

    private void PrintConfig()
    {
        Dictionary<string, string> idToName = new();
        Console.WriteLine("___Source Directories___");
        foreach (SyncedConfigDirectory dir in _syncedConfig.GetSourceDirectories())
        {
            idToName.Add(dir.Id, dir.FolderName);
            Console.WriteLine(dir.ToString());
        }
        Console.WriteLine("\n___Target Directories___");
        foreach (SyncedConfigDirectory dir in _syncedConfig.GetTargetDirectories())
        {
            idToName.Add(dir.Id, dir.FolderName);
            Console.WriteLine(dir.ToString());
        }
        Console.WriteLine("\n___Synced Directories___");
        foreach (SyncedConfigSyncedDirectory dir in _syncedConfig.SyncedDirectories)
        {
            Console.WriteLine(dir.ToString(idToName));
        }
    }

    private string _getFullFilename(string file)
    {
        return new FileInfo(file).FullName;
    }
    
    public void Show()
    {
        while (true)
        {
            Console.WriteLine("SymDirs");
            Console.WriteLine("-------");
            Console.WriteLine();
            PrintConfig();
            Console.WriteLine();
            Console.WriteLine();
            InstructionHelper.PrintInstructions(availableActions);
            Console.Write("Action: ");
            string? read = Console.ReadLine();
            List<string> actions = read == null ? [] : read.Split(',').ToList();
            string action = actions[0].Trim();
            actions.RemoveAt(0);
            Console.WriteLine();
            if (action == "0")
            {
                Console.Write("Enter path to synced config: ");
                string? syncedConfigPath = Console.ReadLine();
                if (syncedConfigPath == null)
                {
                    Console.WriteLine("Invalid path.");
                    continue;
                }

                if (!File.Exists(syncedConfigPath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Synced config file does not exist, creating new one.");
                    Console.ResetColor();
                }

                syncedConfigPath = _getFullFilename(syncedConfigPath);
                _config.SyncedConfigPath = syncedConfigPath;
                _syncedConfig = SyncedConfig.Load(_config.SyncedConfigPath, _localConfig);
                _config.Save();
                break;
            }
            if(_syncedConfig == null) 
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No shared config loaded. Please load a shared config first.");
                Console.ResetColor();
                continue;
            }
            if (action == "1")
            {
                Console.Write("Enter path to local config: ");
                string? localConfigPath = Console.ReadLine();
                if (localConfigPath == null)
                {
                    Console.WriteLine("Invalid path.");
                    continue;
                }

                if (!File.Exists(localConfigPath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Local config file does not exist, creating new one.");
                    Console.ResetColor();
                }
                _config.LocalConfigPath = _getFullFilename(localConfigPath);
                _localConfig = LocalConfig.Load(_config.LocalConfigPath);
                // reload synced config so new local config is applied
                _syncedConfig = SyncedConfig.Load(_config.SyncedConfigPath, _localConfig);
                _config.Save();
                break;
            }
            switch (action.Length > 0 ? action[0] : ' ')
            {
                case '5':
                    Console.WriteLine("Create Target (1) or Source (2) directory?");
                    string? createType = Console.ReadLine()?.Trim();
                    if (createType == null || (createType != "1" && createType != "2"))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Invalid input.");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine("Enter path to directory:");
                    string? path = Console.ReadLine();
                    if (path == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Invalid path.");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine(createType == "1");
                    BooleanMessage msg = createType == "1"
                        ? _syncedConfig.AddTargetDirectory(path)
                        : _syncedConfig.AddSourceDirectory(path);
                    if (!msg.Success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error adding directory: {msg.Message}");
                        Console.ResetColor();
                        continue;
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(msg.Message);
                    Console.ResetColor();
                    _syncedConfig.Save();
                    break;
                case '6':
                    Console.WriteLine("Do you want to add a link (1) or remove an existing link (2)?");
                    string? linkAction = Console.ReadLine()?.Trim();
                    if (linkAction == null || (linkAction != "1" && linkAction != "2"))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Invalid input.");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine("Enter the ID of the source directory:");
                    string? sourceId = Console.ReadLine()?.Trim();
                    if (sourceId == null || sourceId.Trim() == string.Empty)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Invalid ID.");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine("Enter the ID of the target directory:");
                    string? targetId = Console.ReadLine()?.Trim();
                    if (targetId == null || targetId.Trim() == string.Empty)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Invalid ID.");
                        Console.ResetColor();
                        continue;
                    }

                    BooleanMessage linkMsg = linkAction == "1"
                        ? _syncedConfig.CreateLink(sourceId, targetId, true, false)
                        : _syncedConfig.RemoveLink(sourceId, targetId, true);
                    if(!linkMsg.Success) 
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error linking directories: {linkMsg.Message}");
                        Console.ResetColor();
                        continue;
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(linkMsg.Message);
                    Console.ResetColor();
                    _syncedConfig.Save();
                    break;
                case '7':
                    new MountManager().UpdateMounts(FsTabGenerator.Generate(_syncedConfig));
                    break;
                case '8':
                    SyncController syncController = new SyncController();
                    syncController.RestoreFolderMarkerAssistant(_syncedConfig);
                    break;
                case '9':
                    MainWindow mw = new MainWindow();
                    mw.Show();
                    return;
            }
        }
    }
}