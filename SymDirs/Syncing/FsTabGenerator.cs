using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SymDirs.Syncing;

public class FsTabGenerator
{
    const string StartMarker = "#SYMDIRS_AUTO_MOUNTS_START";
    const string EndMarker = "#SYMDIRS_AUTO_MOUNTS_END";
    public const string SymDirsMarkerSuffix = "-bind-mount-tmp";

    private static List<FsTabGeneratorResultDirectory> _getMountedSymDirsFolders(SyncedConfig config)
    {
        string[] mountedFileSystems = File.ReadAllText("/proc/mounts").Split('\n');
        List<FsTabGeneratorResultDirectory> mountedSymDirs = new ();
        foreach (SyncedConfigDirectory targetDir in config.GetTargetDirectories())
        {
            string? dir = targetDir.GetRootDirectoryForIndexingOperations();
            if (dir == null) continue;
            dir = dir.TrimEnd(Path.DirectorySeparatorChar);
            foreach (string mountedDir in mountedFileSystems)
            {
                string[] mountParts = mountedDir.Split(' ');
                if (mountParts.Length < 2) continue;
                string mDir = mountParts[1].Replace("\\040", " ");
                if (!mDir.StartsWith(dir)) continue;
                foreach (SyncedConfigDirectory possibleSourceDir in config.GetSourceDirectories())
                {
                    targetDir.SetSyncedWithDirectory(possibleSourceDir);
                    string? possiblePath = targetDir.GetRootDirectoryForSyncingOperations();
                    if (possiblePath == null) continue;
                    possiblePath = possiblePath.TrimEnd(Path.DirectorySeparatorChar);
                    if (possiblePath == mDir) break;
                }
                mountedSymDirs.Add(new ()
                {
                    Path = mDir,
                    SyncedDirectory = targetDir
                });
            }    
        }

        return mountedSymDirs;
    }
    
    public static FsTabGeneratorResult Generate(SyncedConfig config)
    {
        string existingFsTab = File.ReadAllText("/etc/fstab");
        List<string> normalMounts = new List<string>();
        // existing mounts will contain all mountpoints which need to be manually unmounted
        List<FsTabGeneratorResultDirectory> existingSymDirsMounts = _getMountedSymDirsFolders(config);
        bool ignore = false;
        foreach (var line in existingFsTab.Split(Environment.NewLine))
        {
            if (line.StartsWith(StartMarker))
            {
                ignore = true;
                continue;
            }
            if (line.StartsWith(EndMarker))
            {
                ignore = false;
                continue;
            }

            if (ignore) continue;
            normalMounts.Add(line);
        }

        List<string> newSymDirsEntries = new();
        foreach (SyncedConfigDirectory source in config.GetSourceDirectories())
        {
            string? sourcePath = source.GetRootDirectoryForSyncingOperations();
            if (sourcePath == null) continue;
            foreach (SyncedConfigDirectory target in config.GetLinkedDirectories(source))
            {
                if (target.IsSourceDirectory) continue;
                string? targetPath = target.GetRootDirectoryForSyncingOperations();
                if (targetPath == null) continue;
                targetPath = targetPath.TrimEnd(Path.DirectorySeparatorChar);
                sourcePath = sourcePath.TrimEnd(Path.DirectorySeparatorChar);
                // bind mount with option to hide it in the file manager
                string start = $"{sourcePath.Replace(" ", "\\040")} {targetPath.Replace(" ", "\\040")}";
                for (int i = 0; i < existingSymDirsMounts.Count; i++)
                {
                    if (existingSymDirsMounts[i].Path != targetPath) continue;
                    existingSymDirsMounts.RemoveAt(i);
                    i--;
                }

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                    FolderMarker.CreateDirectoryMarker(targetPath, target.Id + SymDirsMarkerSuffix);
                }
                newSymDirsEntries.Add(start + " none rw,bind,x-gvfs-hide 0 0"); 
            }
        }
        string newFsTab = string.Join(Environment.NewLine, normalMounts);
        newFsTab += "\n" + StartMarker + "\n" + string.Join(Environment.NewLine, newSymDirsEntries) + "\n" + EndMarker;
        return new FsTabGeneratorResult
        {
            FsTab = newFsTab,
            NeedUnmount = existingSymDirsMounts
        };
    }
}

public class FsTabGeneratorResult
{
    public string FsTab { get; set; }
    public List<FsTabGeneratorResultDirectory> NeedUnmount = new ();

    public IEnumerable<FsTabGeneratorResultDirectory> GetUnmountCommands(bool lazyUnmount)
    {
        foreach (FsTabGeneratorResultDirectory path in NeedUnmount)
        {
            ProcessStartInfo info = new ProcessStartInfo("umount");
            if(lazyUnmount) info.ArgumentList.Add("-l");
            info.ArgumentList.Add(path.Path);
            info.RedirectStandardError = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            path.UnmountCommand = info;
            yield return path;
        }
    }

    public override string ToString()
    {
        return "/etc/fstab:\n" + FsTab + "\n\nMounted paths which are currently mounted and need to get unmounted:\n" + String.Join('\n',
            NeedUnmount);
    }
}

public class FsTabGeneratorResultDirectory
{
    public string Path;
    public SyncedConfigDirectory SyncedDirectory;
    public ProcessStartInfo? UnmountCommand;
}