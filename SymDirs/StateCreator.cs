using System.Diagnostics;

namespace SymDirs;

/// <summary>
/// Creates the state requested by a configuration.
/// </summary>
public class StateCreator
{
    public static int UpdateSubdirsInConfig(Config config)
    {
        int added = 0;
        foreach (string folder in config.SourceDirectorySources)
        {
            foreach (string dir in Directory.GetDirectories(folder))
            {
                added += config.AddSource(dir);
            }
        }

        return added;
    }
    public static void CheckState(Config config)
    {
        config.UpdateRelations();
        config.TargetDirectories.ForEach(x => x.MissingContent.Clear());
        foreach (ConfigDirectory sourceDirectory in config.SourceDirectories)
        {
            if (sourceDirectory.Path == null || sourceDirectory.Name == null) continue;
            foreach (ConfigDirectory targetDirectory in config.TargetDirectories)
            {
                if (targetDirectory.Path == null) continue;
                bool linked = sourceDirectory.Links.Contains(targetDirectory);
                string targetPath = Path.Combine(targetDirectory.Path, sourceDirectory.Name);
                if (!linked) continue;
                targetDirectory.MissingContent.Add(sourceDirectory.Path, CheckState(targetPath, sourceDirectory.Path));
            }
        }
    }
    
    public static void ApplyState(Config config)
    {
        config.UpdateRelations();
        Dictionary<ConfigDirectory, List<string>> newFiles = new();
        foreach (ConfigDirectory sourceDirectory in config.SourceDirectories)
        {
            if (sourceDirectory.Path == null || sourceDirectory.Name == null) continue;
            foreach (ConfigDirectory targetDirectory in config.TargetDirectories)
            {
                if (targetDirectory.Path == null) continue;
                bool linked = sourceDirectory.Links.Contains(targetDirectory);
                string targetPath = Path.Combine(targetDirectory.Path, sourceDirectory.Name);
                if (linked)
                {
                    LinkAllFiles(targetPath, sourceDirectory.Path, sourceDirectory, targetDirectory, newFiles, config);
                    continue;
                }

                if (!Directory.Exists(targetPath)) continue;
                Directory.Delete(targetPath, true);
            }
        }

        foreach (KeyValuePair<ConfigDirectory,List<string>> pair in newFiles)
        {
            pair.Key.presentFiles = pair.Value;
        }

        CheckState(config);
    }
    
    public static List<string> CheckState(string targetDirectory, string sourceDirectory)
    {
        // Link all files
        List<string> missingFiles = new List<string>();
        foreach (string newPath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            string newFilePath = newPath.Replace(sourceDirectory, targetDirectory);
            if (File.Exists(newFilePath)) continue;
            missingFiles.Add(newFilePath);
        }

        return missingFiles;
    }

    public static void LinkAllFiles(string targetDirectory, string sourceDirectory, ConfigDirectory sourceDir, ConfigDirectory targetDir, Dictionary<ConfigDirectory, List<string>> newFiles, Config c)
    {
        if(!newFiles.ContainsKey(sourceDir)) newFiles[sourceDir] = new List<string>();
        foreach (string newPath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            string newFilePath = newPath.Replace(sourceDirectory, targetDirectory);
            string file = newPath.Replace(sourceDirectory, "");
            if (file.StartsWith("/")) file = file.Substring(1);
            if(!newFiles[sourceDir].Contains(file)) newFiles[sourceDir].Add(file);
            
            if (File.Exists(newFilePath)) continue;
            string directory = Path.GetDirectoryName(newFilePath);
            if (directory == null) continue;
            Directory.CreateDirectory(directory);
            Process.Start("ln", [newPath,newFilePath]);
        }
        foreach (string file in sourceDir.presentFiles)
        {
            if (newFiles[sourceDir].Contains(file)) continue;
            string newFile = Path.Combine(targetDirectory, file);
            Console.WriteLine(newFile);
            if (!File.Exists(newFile)) continue;
            File.Delete(newFile);
        }
        
        
        if (!c.AllowTargetToSourceFileAdding) return;
        
        foreach (string newPath in Directory.GetFiles(targetDirectory, "*.*", SearchOption.AllDirectories))
        {
            string file = newPath.Replace(targetDirectory, "");
            if (file.StartsWith("/")) file = file.Substring(1);
            
            if(!newFiles[sourceDir].Contains(file)) newFiles[sourceDir].Add(file);
            
            string newFilePath = newPath.Replace(targetDirectory, sourceDirectory);
            if(sourceDir.presentFiles.Contains(file)) continue;
            // we found a file that is not in the source directory and has not been deleted since the last sync. This means we copy it to the source directory.
            string directory = Path.GetDirectoryName(newFilePath);
            if (directory == null) continue;
            Directory.CreateDirectory(directory);
            Process.Start("ln", [newPath, newFilePath]);
        }
    }
}