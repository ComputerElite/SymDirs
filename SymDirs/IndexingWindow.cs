using System.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using SymDirs.Db;
using SymDirs.Index;
using SymDirs.Syncing;

namespace SymDirs;

public class IndexingWindow
{
    public Config config;
    public SyncedConfig sharedConfig;
    public LocalConfig localConfig;
    public IndexingWindow(Config config) 
    {
        this.config = config;
    }
    
    public static Dictionary<string, string> availableActions = new()
    {
        {"1", "Index directory and save to db"},
        {"2", "Mock Sync (moves all files from changed to synced)"},
        {"3", "Apply database migrations"},
        {"4", "Set shared config path"},
        {"9", "Main Menu"},
    };
    
    public void Show()
    {
        localConfig = LocalConfig.Load(config.LocalConfigPath);
        while (true)
        {
            Console.WriteLine("SymDirs");
            Console.WriteLine("-------");
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
                    Console.Write("Enter directory to index: ");
                    string? dir = Console.ReadLine();
                    if (dir == null || !Directory.Exists(dir))
                    {
                        Console.WriteLine("Invalid directory.");
                        continue;
                    }
                    FileIndexer indexer = new FileIndexer();
                    List<DbFile> files = indexer.IndexDirectory(dir);
                    using (var db = new Database())
                    {
                        db.ChangedFiles.AddRange(files);
                        db.SaveChanges();
                    }
                    Console.WriteLine("Directory indexed and saved to database.");
                    break;
                case '2':
                    using (var db = new Database())
                    {
                        List<DbFile> changedFiles = db.ChangedFiles.ToList();
                        Console.WriteLine("Updating file state of " + changedFiles.Count + " files.");
                        foreach (DbFile file in changedFiles)
                        {
                            file.State = DbFileState.Synced;
                        }
                        db.SyncedFiles.AddRange(changedFiles);
                        db.ChangedFiles.RemoveRange(changedFiles);
                        db.SaveChanges();
                    }
                    Console.WriteLine("Mock sync completed.");
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
                    sharedConfig = SyncedConfig.Load(syncedConfigPath, localConfig);
                    break;
                case '9':
                    return;
            }
        }
    }
}