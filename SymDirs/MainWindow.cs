using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace SymDirs;

public class MainWindow
{
    public string state = "";
    public Config? config;

    public static Dictionary<string, string> availableActions = new()
    {
        {"1[,y/n whether to add subdirectories of the supplied directory],<source_dir>", "Add source directory"},
        {"2,<target_directory>", "Add target directory"},
        {"3,<e/d whether to enable or disable the link><range of source dirs>,<range of target dirs>", "toggle link"},
        {"4", "Apply state (fix partial content)"},
        {"5", "Reload configuration"},
        {"6,<range of source dirs>", "Remove source directory"},
        {"7,<range of target dirs>", "Remove target directory"},
        {"8", "Update sources based on subdirectories"},
        {"9", "Settings"}
    };

    private const string rangeHelp = "Range examples e. g. ('0-6', '2 3 6'). Alternatively a part of the directory path (e. g. 'beastars season 1')";

    public void Show(string arg = "")
    {
        if(config == null) config = Config.Load();
        if (arg == "")
        {
            Console.ForegroundColor = ConsoleColor.White;
            //Console.Clear();
            //Console.SetCursorPosition(0, 0);
        }
        Console.WriteLine("SymDirs");
        Console.WriteLine("-------");
        Console.WriteLine();
        Console.WriteLine(state);
        Console.WriteLine();
        ListState();
        string availableActionsString = "";
        foreach (KeyValuePair<string,string> keyValuePair in availableActions)
        {
            availableActionsString += $"[{keyValuePair.Key.Substring(0, 1)}] {keyValuePair.Value}{(keyValuePair.Key.StartsWith("4") ? "\n" : "  ")}";
        }
        Console.WriteLine(availableActionsString);
        Console.Write("Action: ");
        string? read = arg != "" ? arg : Console.ReadLine();
        List<string> actions = read == null ? [] : read.Split(',').ToList();
        string action = actions[0];
        actions.RemoveAt(0);
        Console.WriteLine();
        state = "";
        
        switch (action.Length > 0 ? action[0] : ' ')
        {
            case '1':
                Console.Write("Add subdirectories? (Y/n): ");
                bool subdirs = actions.Count > 1 ? actions[0] != "n" : Console.ReadKey().KeyChar != 'n';
                if(actions.Count > 1 && !actions[0].StartsWith("/")) actions.RemoveAt(0);
                Console.WriteLine();
                Console.Write("Path: ");
                string path = actions.Count >= 1 ? String.Join(',', actions) : Console.ReadLine();
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
                SaveAndApply();
                state = $"Added {added} source {(added == 1 ? "directory" : "directories")}";
                break;
            case '2':
                Console.Write("Path: ");
                string path2 = actions.Count >= 1 ? String.Join(',', actions) : Console.ReadLine();
                if (path2.Trim() == "") break;
                state = $"Added {config.AddTarget(path2)} target directory";
                SaveAndApply();
                break;
            case '3':
                string expression;
                if (actions.Count < 2)
                {
                    Console.Write("Source: ");
                    expression = Console.ReadLine();
                }
                else expression = String.Join(',', actions);
                
                bool? enable = null;
                try
                {
                    
                    bool e;
                    if ((e = expression.StartsWith("e")) || expression.StartsWith("d"))
                    {
                        enable = e;
                        expression = expression.Substring(1);
                    }
                    List<int> sources = new List<int>();
                    List<int> targets = new List<int>();
                    if (expression.Contains(","))
                    {
                        sources.AddRange(ParseRange(expression.Split(",")[0], true));
                        targets.AddRange(ParseRange(expression.Split(",")[1], false));
                    }
                    else
                    {
                        sources.Add(int.Parse(expression));
                        Console.Write("Target: ");
                        targets.Add(int.Parse(Console.ReadLine()));
                    }

                    foreach (int source in sources)
                    {
                        foreach (int target in targets)
                        {
                            if (source < config.SourceDirectories.Count && target < config.TargetDirectories.Count)
                            {
                                ConfigDirectory sourceDir = config.SourceDirectories[source];
                                ConfigDirectory targetDir = config.TargetDirectories[target];
                                if (enable == null && sourceDir.Links.Contains(targetDir) || enable.HasValue && !enable.Value)
                                {
                                    sourceDir.Remove(targetDir);
                                }
                                else
                                {
                                    sourceDir.Add(targetDir);
                                }
                            }
                        }
                    }
                    SaveAndApply();
                } catch (Exception e)
                {
                    state = e.ToString();
                }
                
                break;
            case '4':
                SaveAndApply();
                state = "Applied state to disk";
                break;
            case '5':
                config = Config.Load();
                state = "Loaded config from disk";
                break;
            case '6':
                try
                {
                    string expression2;
                    if (actions.Count <= 1)
                    {
                        Console.Write("Source: ");
                        expression2 = Console.ReadLine();
                    }
                    else expression2 = actions[0];

                    foreach (int removeSource in ParseRange(expression2))
                    {
                        if (removeSource >= config.SourceDirectories.Count) return;
                        config.SourceDirectories.RemoveAt(removeSource);
                    }
                    SaveAndApply();
                } catch (Exception e)
                {
                    state = e.ToString();
                }
                break;
            case '7':
                try
                {
                    string expression2;
                    if (actions.Count <= 1)
                    {
                        Console.Write("Target: ");
                        expression2 = Console.ReadLine();
                    }
                    else expression2 = actions[0];
                    foreach (int removeTarget in ParseRange(expression2))
                    {
                        if (removeTarget >= config.TargetDirectories.Count) return;
                        config.TargetDirectories.RemoveAt(removeTarget);
                    }
                    SaveAndApply();
                }
                catch (Exception e)
                {
                    state = e.ToString();
                }
                break;
            case '8':
                state = $"Added {StateCreator.UpdateSubdirsInConfig(config)} new source directories from {config.SourceDirectorySources.Count} directories";
                SaveAndApply();
                break;
            case '9':
                Settings();
                break;
        }
        if (arg != "") Console.WriteLine("\n" + state);
    }

    private void Settings()
    {
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("---Settings---");
        Console.WriteLine();
        Console.WriteLine("[0] Toggle Sync new files in target directories with source directories");
        Console.WriteLine("       -> currently: " + config.AllowTargetToSourceFileAdding);
        Console.WriteLine();
        
        Console.Write("Action: ");
        string read = Console.ReadLine();
        switch (read.Length > 0 ? read[0] : ' ')
        {
            case '0':
                config.AllowTargetToSourceFileAdding = !config.AllowTargetToSourceFileAdding;
                state = "Option is now " + config.AllowTargetToSourceFileAdding;
                config.Save();
                break;
        }
    }

    private void SaveAndApply()
    {
        config.UpdateRelations();
        StateCreator.ApplyState(config);
        config.Save();
    }

    public List<int> ParseRange(string range, bool useSources = true)
    {
        List<int> sources = new List<int>();
        foreach(string part in range.Split(" "))
        {
            if(part.Trim() == "") continue;
            
            if (!Regex.IsMatch(part.Trim(), @"^[0-9]+$"))
            {
                // Perhaps it's a directory
                ICollection<ConfigDirectory> dirs =
                    (useSources ? config?.SourceDirectories : config?.TargetDirectories) ?? [];
                for (int i = 0; i < dirs.Count; i++)
                {
                    if (!dirs.ElementAt(i).Path.Contains(part)) continue;
                    sources.Add(i);
                }
                continue;
            }
            
            string[] partialParts = part.Split('-');
            if (partialParts.Length > 1)
            {
                for(int i = int.Parse(partialParts[0]); i <= int.Parse(partialParts[1]); i++)
                {
                    sources.Add(i);
                }

                continue;
            }
            sources.Add(int.Parse(part));
        }

        return sources;
    }

    public void ListState()
    {
        if ((config?.SourceDirectorySources.Count ?? 0) > 0)
        {
            Console.WriteLine("Folders to scan for source directories:");
            foreach (string folder in config?.SourceDirectorySources ?? [])
            {
                Console.WriteLine(folder);
            }
            Console.WriteLine();
        }
        
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
                bool partialContent = targetDirectory.MissingContent.ContainsKey(sourceDir.Path ?? "") && targetDirectory.MissingContent[sourceDir.Path ?? ""].Count > 0;
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

    public static void ShowHelp()
    {
        Console.WriteLine("SymDirs <short_code>");
        Console.WriteLine();
        Console.WriteLine("SymDir short codes allow for quick typing of the action you wanna do.");
        Console.WriteLine("Arguments in short codes are seperated via commas (,).");
        Console.WriteLine("Following actions are available:");
        foreach (KeyValuePair<string,string> availableAction in availableActions)
        {
            Console.WriteLine(availableAction.Key + ": " + availableAction.Value);
        }
        Console.WriteLine();
        Console.WriteLine(rangeHelp);
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("'3,e0-3,1 3'     Link source folder 0, 1, 2, 3 to target folders 1 and 3");
        Console.WriteLine("'1,y,/home/user/media/beastars'     Add all subdirectories of '/home/user/media/beastars season 1' as source directories");
    }
}