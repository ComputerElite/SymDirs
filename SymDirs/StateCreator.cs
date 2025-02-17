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
                    LinkAllFiles(targetPath, sourceDirectory.Path);
                    continue;
                }

                if (!Directory.Exists(targetPath)) continue;
                Directory.Delete(targetPath, true);
            }
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

    public static void LinkAllFiles(string targetDirectory, string sourceDirectory)
    {
        // Link all files
        foreach (string newPath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            string newFilePath = newPath.Replace(sourceDirectory, targetDirectory);
            if (File.Exists(newFilePath)) continue;
            string directory = Path.GetDirectoryName(newFilePath);
            if (directory == null) continue;
            Directory.CreateDirectory(directory);
            Process.Start("ln", [newPath,newFilePath]);
        }
    }
}