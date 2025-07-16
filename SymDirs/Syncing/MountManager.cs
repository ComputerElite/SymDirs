using System.Diagnostics;
using System.Net.Mime;
using Microsoft.Extensions.Logging;

namespace SymDirs.Syncing;

public class MountManager
{
    private ILogger _logger;
    public MountManager()
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger("MountManager");
    }
    private int _invokeProcess(ProcessStartInfo info)
    {
        Process? uMountProcess = Process.Start(info);
        if(uMountProcess == null) return -1;
        uMountProcess.WaitForExit();
        string output = uMountProcess.StandardOutput.ReadToEnd();
        output += uMountProcess.StandardError.ReadToEnd();
        _logger.LogInformation(output);
        if (uMountProcess.ExitCode == 32)
        {
            _logger.LogError("Couldn't unmount directory due to lack of permission for umount");
            return uMountProcess.ExitCode;
        }
        if (uMountProcess.ExitCode != 0)
        {     
            _logger.LogError("Couldn't unmount directory due to an unknown error:" + output);
            return uMountProcess.ExitCode;
        }
        Console.WriteLine(output);
        return uMountProcess.ExitCode;
    }
    public void UpdateMounts(FsTabGeneratorResult res)
    {
        _logger.LogInformation($"Updating mounts based on FsTabGeneratorResult: {res}");
        _logger.LogInformation($"Updating fstab...");
        try
        {
            File.Copy("/etc/fstab", "/etc/fstab.bak", true);
            File.WriteAllText("/etc/fstab", res.FsTab);
        }
        catch (Exception e)
        {
            _logger.LogError("Couldn't update fstab due to an unknown error:" + e);
            return;
        }
        _logger.LogInformation($"Unmounting directories...");
        // ToDo: Make sure directories for the mountpoints exists.
        foreach (FsTabGeneratorResultDirectory command in res.GetUnmountCommands(true))
        {
            string path = command.Path;
            if(command.UnmountCommand == null) continue;
            _logger.LogInformation(String.Join(" ", command.UnmountCommand.ArgumentList));
            if(_invokeProcess(command.UnmountCommand) != 0)
            {
                return;
            }
            // if the unmount succeeded we delete the directory if it contains a directory marker
            string? marker = FolderMarker.GetIdOfDirectory(path);
            if (marker != command.SyncedDirectory.Id + FsTabGenerator.SymDirsMarkerSuffix)
            {
                // the directory was not created solely for mounting, do nothing
                continue;
            }
            _logger.LogInformation($"Deleting directory {command.Path} as the folder marker is {marker}.");
            Directory.Delete(command.Path, true);
        }
        _logger.LogInformation("Reloading daemon to use new /etc/fstab");
        // ToDo: Apply only the mount operations actually needed instead of applying everything from the fstab
        if (_invokeProcess(new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "daemon-reload",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }) != 0)
        {
            return;
        }
        _logger.LogInformation("Mounting everything from fstab");
        if(_invokeProcess(new ProcessStartInfo
        {
            FileName = "mount",
            Arguments = "-a",
            RedirectStandardError =true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) != 0)
        {
            return;
        }
    }

    /// <summary>
    /// Updates mountpoints
    /// </summary>
    /// <param name="configs"></param>
    public static void Update(ConfigContext configs)
    {
        new MountManager().UpdateMounts(FsTabGenerator.Generate(configs.SyncedConfig));
    }

    /// <summary>
    /// Updated the mount points. If watch is provided this method will never exit and update mounts once configs change
    /// </summary>
    /// <param name="configs"></param>
    /// <param name="watch"></param>
    public static void Update(ConfigContext configs, bool watch)
    {
        if (!watch)
        {
            Update(configs);
            return;
        }
        configs.StartFilesystemWatchers();
        configs.OnConfigsUpdated = Update;
        Thread.Sleep(Timeout.Infinite);
    }
}