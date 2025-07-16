using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SymDirs.Db;
using SymDirs.Index;
using SymDirs.ReturnTypes;
using SymDirs.Syncing;

namespace SymDirs;

public class IndexingWindow
{
    public Config config;
    public SyncedConfig syncedConfig;
    public LocalConfig localConfig;
    public IndexingWindow(Config config) 
    {
        this.config = config;
    }
    
    public static Dictionary<string, string> availableActions = new()
    {
        {"1", "Index directory and save to db"},
        {"2", "Sync"},
        {"3", "Apply database migrations"},
        {"4", "Set shared config path"},
        {"5", "Add directory"},
        {"6", "Update link"},
        {"7", "Generate /etc/fstab"},
        {"8", "Fix folder markers"},
        {"9", "Main Menu"},
    };

    public void PrintConfig()
    {
        Dictionary<string, string> idToName = new();
        Console.WriteLine("___Source Directories___");
        foreach (SyncedConfigDirectory dir in syncedConfig.GetSourceDirectories())
        {
            idToName.Add(dir.Id, dir.FolderName);
            Console.WriteLine(dir.ToString());
        }
        Console.WriteLine("\n___Target Directories___");
        foreach (SyncedConfigDirectory dir in syncedConfig.GetTargetDirectories())
        {
            idToName.Add(dir.Id, dir.FolderName);
            Console.WriteLine(dir.ToString());
        }
        Console.WriteLine("\n___Synced Directories___");
        foreach (SyncedConfigSyncedDirectory dir in syncedConfig.SyncedDirectories)
        {
            Console.WriteLine(dir.ToString(idToName));
        }
        Console.WriteLine("\n___Database___");
        using (Database db = new())
        {
            Console.WriteLine($"Changed files: {db.Files.Count(x => !x.IsSynced)}");
            Console.WriteLine($"Synced files: {db.Files.Count(x => x.IsSynced)}");
        }
    }
    
    public void Show()
    {
        localConfig = LocalConfig.Load(config.LocalConfigPath);
        syncedConfig = SyncedConfig.Load(config.SyncedConfigPath, localConfig);
        
        using (var db = new Database())
        {
            db.Database.Migrate();
        }
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
            string action = actions[0];
            actions.RemoveAt(0);
            Console.WriteLine();
            switch (action.Length > 0 ? action[0] : ' ')
            {
                case '1':
                    if (syncedConfig == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No shared config loaded. Please load a shared config first.");
                        Console.ResetColor();
                        continue;
                    }
                    FileIndexer indexer = new FileIndexer();
                    indexer.IndexConfig(syncedConfig);
                    Console.WriteLine("Directory indexed and saved to database.");
                    break;
                case '2':
                    if (syncedConfig == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No shared config loaded. Please load a shared config first.");
                        Console.ResetColor();
                        continue;
                    }
                    SyncController creator = new SyncController();
                    creator.ProcessChanges(syncedConfig, true);
                    Console.WriteLine("\n\nDo you want to apply these changes? (y/N): ");
                    string? answer = Console.ReadLine();
                    if (answer != "y") break;
                    creator.ProcessChanges(syncedConfig, false);
                    break;
                case '3':
                    using (var db = new Database())
                    {
                        db.Database.Migrate();
                    }
                    Console.WriteLine("Database migrations applied.");
                    break;
                case '4':
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
                    config.SyncedConfigPath = syncedConfigPath;
                    syncedConfig = SyncedConfig.Load(syncedConfigPath, localConfig);
                    break;
                case '5':
                    if(syncedConfig == null) 
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No shared config loaded. Please load a shared config first.");
                        Console.ResetColor();
                        continue;
                    }
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
                        ? syncedConfig.AddTargetDirectory(path)
                        : syncedConfig.AddSourceDirectory(path);
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
                    syncedConfig.Save();
                    break;
                case '6':
                    if (syncedConfig == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No shared config loaded. Please load a shared config first.");
                        Console.ResetColor();
                        continue;
                    }

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
                        ? syncedConfig.CreateLink(sourceId, targetId, true)
                        : syncedConfig.RemoveLink(sourceId, targetId, true);
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
                    syncedConfig.Save();
                    break;
                case '7':
                    if (syncedConfig == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No shared config loaded. Please load a shared config first.");
                        Console.ResetColor();
                        continue;
                    }

                    new MountManager().UpdateMounts(FsTabGenerator.Generate(syncedConfig));

                    //FileIndexer i = new FileIndexer();
                    //i.StartFilesystemWatcher(syncedConfig);
                    break;
                case '8':
                    if (syncedConfig == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No shared config loaded. Please load a shared config first.");
                        Console.ResetColor();
                        continue;
                    }
                    SyncController syncController = new SyncController();
                    syncController.RestoreFolderMarkerAssistant(syncedConfig);
                    break;
                case '9':
                    return;
            }
        }
    }
}