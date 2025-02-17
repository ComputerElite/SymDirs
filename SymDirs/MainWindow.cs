namespace SymDirs;

public class MainWindow
{
    public string state = "";
    public Config? config;
    public void Show()
    {
        if(config == null) config = Config.Load();
        Console.ForegroundColor = ConsoleColor.White;
        Console.Clear();
        Console.SetCursorPosition(0, 0);
        Console.WriteLine("SymDirs");
        Console.WriteLine("-------");
        Console.WriteLine();
        Console.WriteLine(state);
        Console.WriteLine();
        ListState();
        Console.WriteLine("[1] Add source directory     [2] Add target directory  [3] toggle link  [4] Apply & Save configuration  [5] Reload configuration");
        Console.WriteLine("[6] Remove source directory  [7] Remove target directory");
        Console.Write("Action: ");
        ConsoleKeyInfo read = Console.ReadKey();
        Console.WriteLine();
        state = "";
        
        switch (read.KeyChar)
        {
            case '1':
                Console.Write("Add subdirectories? (Y/n): ");
                bool subdirs = Console.ReadKey().KeyChar != 'n';
                Console.WriteLine();
                Console.Write("Path: ");
                string path = Console.ReadLine();
                if (path.Trim() == "") break;
                int added = 0;
                if (subdirs)    
                {
                    Console.WriteLine(path);
                    config.SourceDirectorySources.Add(path);
                    foreach (string dir in Directory.GetDirectories(path))
                    {
                        added += config.AddSource(dir);
                    }
                }
                else
                {
                    added += config.AddSource(path);
                }
                config.UpdateRelations();
                state = $"Added {added} source {(added == 1 ? "directory" : "directories")}";
                break;
            case '2':
                Console.Write("Path: ");
                string path2 = Console.ReadLine();
                if (path2.Trim() == "") break;
                state = $"Added {config.AddTarget(path2)} target directory";
                config.UpdateRelations();
                break;
            case '3':
                Console.Write("Source: ");
                int source = int.Parse(Console.ReadLine());
                Console.Write("Target: ");
                int target = int.Parse(Console.ReadLine());
                if (source < config.SourceDirectories.Count && target < config.TargetDirectories.Count)
                {
                    ConfigDirectory sourceDir = config.SourceDirectories[source];
                    ConfigDirectory targetDir = config.TargetDirectories[target];
                    if (sourceDir.Links.Contains(targetDir))
                    {
                        sourceDir.Remove(targetDir);
                    }
                    else
                    {
                        sourceDir.Add(targetDir);
                    }
                    config.UpdateRelations();
                }
                break;
            case '4':
                config.Save();
                StateCreator.ApplyState(config);
                state = "Applied state to disk";
                break;
            case '5':
                config = Config.Load();
                state = "Loaded config from disk";
                break;
            case '6':
                Console.Write("Source: ");
                int removeSource = int.Parse(Console.ReadLine());
                if (removeSource < config.SourceDirectories.Count)
                {
                    config.SourceDirectories.RemoveAt(removeSource);
                    config.UpdateRelations();
                }
                break;
            case '7':
                Console.Write("Target: ");
                
                int removeTarget = int.Parse(Console.ReadLine());
                if (removeTarget < config.TargetDirectories.Count)
                {
                    config.TargetDirectories.RemoveAt(removeTarget);
                    config.UpdateRelations();
                }
                break;
        }
    }

    public void ListState()
    {
        int longestFolder = 0;
        foreach (var dir in config?.SourceDirectories ?? [])
        {
            if (dir.Path == null) continue;
            if (dir.NameLength > longestFolder) longestFolder = dir.NameLength;
        }

        int longestNumber = config?.SourceDirectories.Count.ToString().Length ?? 0;
        longestNumber += 2;
        
        longestFolder += 2;
        int i = 0;
        Console.Write("".PadRight(longestFolder + longestNumber));
        foreach (ConfigDirectory targetDirectory in config?.TargetDirectories ?? [])
        {
            Console.Write(i.ToString().PadLeft(targetDirectory.NameLength / 2).PadRight(targetDirectory.NameLength + 2));
            i++;
        }
        Console.WriteLine();
        Console.Write("".PadRight(longestFolder + longestNumber));
        foreach (ConfigDirectory targetDirectory in config?.TargetDirectories ?? [])
        {
            Console.Write(targetDirectory.Name?.PadRight(targetDirectory.NameLength + 2));
        }
        Console.WriteLine();

        i = 0;
        foreach (ConfigDirectory sourceDir in config?.SourceDirectories ?? [])
        {
            Console.Write(i.ToString().PadRight(longestNumber));
            Console.Write(sourceDir.Name?.PadRight(longestFolder));
            foreach (ConfigDirectory targetDirectory in config?.TargetDirectories ?? [])
            {
                bool linked = sourceDir.Links.Contains(targetDirectory);
                bool partialContent = targetDirectory.MissingOrAddedContent.Count > 0;
                Console.ForegroundColor = linked ? ConsoleColor.Green : ConsoleColor.Red;
                if(linked && partialContent) Console.ForegroundColor = ConsoleColor.Yellow;
                string text = linked ? $"Y{(partialContent ? " (p)" : "")}" : "N";
                Console.Write(text.PadLeft(targetDirectory.NameLength / 2 + text.Length / 2).PadRight(targetDirectory.NameLength + 2));
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            i++;
        }
        Console.WriteLine();
    }
}