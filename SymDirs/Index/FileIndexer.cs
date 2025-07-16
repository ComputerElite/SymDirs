using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SymDirs.Db;
using SymDirs.Syncing;

namespace SymDirs.Index;

public class FileIndexer
{
    public static List<FileSystemWatcher> FileWatchers { get; } = new List<FileSystemWatcher>();
    private byte[] HashFile(string path)
    {
        using (SHA256 hash = SHA256.Create())
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open))
                {
                    return hash.ComputeHash(fs);
                }
            }
            catch (Exception e)
            {
                return []; // Perhaps return null instead???
            }
        }
    }

    /// <summary>
    /// Indexes a file by its path
    /// </summary>
    /// <param name="path">Path of the file you want to index</param>
    /// <param name="rehashIfModifiedDataHasntChanged">Whether to compute the has even if the modified date hasn't changed</param>
    /// <returns></returns>
    private DbFile? IndexFile(string path, bool rehashIfModifiedDataHasntChanged = false, bool returnNullIfUnchanged = true)
    {
        Console.WriteLine($"Indexing {path}");
        using (var db = new Database())
        {
            // 1. Retrieve old file state from the database
            DbFile? oldEntry = db.Files.FirstOrDefault(x => x.IsSynced && x.FullPath == path);
            DbFile file = new DbFile(path);
            if (!File.Exists(path))
            {
                if (oldEntry != null)
                {
                    // If the file does not exist, but it was in the database, mark it as deleted
                    file.State = DbFileState.Deleted;
                    return file;
                }
                // The file does not exist and was not tracked. This should return null then.
                db.Files.Where(x => x.FullPath == path).ExecuteDelete();
                return null;
            }

            FileInfo fileInfo = new FileInfo(path);
            file.LastModified = fileInfo.LastWriteTimeUtc;
            file.ByteSize = fileInfo.Length;
            
            // Only hash the file if it's been modified according to the modified date
            if(file.LastModified != oldEntry?.LastModified
               || file.ByteSize != oldEntry?.ByteSize
               || rehashIfModifiedDataHasntChanged) file.Hash = HashFile(path);
            else file.Hash = oldEntry.Hash;
            
            // The file hasn't been tracked before; therefore, it's new
            if (oldEntry == null)
            {
                file.State = DbFileState.New;
                return file.PopulateInode();
            }
            
            
            // Lastly, if the content changed, we mark it as modified
            if (oldEntry.Hash != file.Hash)
            {
                file.State = DbFileState.Modified;
                return file.PopulateInode();
            }
            
            // If it was tracked before we can just copy the LastSync of the old entry
            file.LastSync = oldEntry.LastSync; // perhaps this is unwanted behavior as a file will then be tracked as already synced. As the returnNullIfUnchanged flag isn't used tho it can be ignored for now
            file.State = DbFileState.Unchanged;
            return returnNullIfUnchanged ? null : file;
        }
    }
    
    public void IndexDirectory(string path, bool rehashIfModifiedDataHasntChanged = false)
    {
        if (!Directory.Exists(path)) return;
        List<DbFile> files = new List<DbFile>();
        List<string> checkedPaths = new List<string>();
        Console.WriteLine($"Indexing directory {path}...");
        foreach (string directoryPath in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
        {
            string dirPath = directoryPath;
            if (!dirPath.EndsWith(Path.DirectorySeparatorChar)) dirPath += Path.DirectorySeparatorChar; 
            Console.WriteLine(dirPath);
            // ToDo: Create DB File
        }
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            Console.WriteLine(filePath);
            checkedPaths.Add(filePath);
            DbFile? indexedFile = IndexFile(filePath, rehashIfModifiedDataHasntChanged);
            if (indexedFile != null) files.Add(indexedFile);
        }

        using (Database db = new Database())
        {
            int deletedDuplicates = db.Files.Where(x => !x.IsSynced && checkedPaths.Contains(x.FullPath)).ExecuteDelete();
            if (deletedDuplicates > 0)
            {
                Console.WriteLine($"Deleted {deletedDuplicates} duplicate entries from the database.");
            }
            db.Files.AddRange(files);
            List<DbFile> uncheckedFiles = db.Files.Where(x => x.FullPath.StartsWith(path) && !checkedPaths.Contains(x.FullPath)).ToList();
            Console.WriteLine($"Checking {uncheckedFiles.Count} unchecked files...");
            foreach (DbFile uncheckedFile in uncheckedFiles)
            {
                Console.WriteLine(uncheckedFile.FullPath);
                DbFile? indexedFile = IndexFile(uncheckedFile.FullPath, rehashIfModifiedDataHasntChanged);

                if (indexedFile != null)
                {
                    db.Files.Remove(uncheckedFile);
                    db.Files.Add(indexedFile);
                }
            }
            db.SaveChanges();
        }
        Console.WriteLine($"Indexed {files.Count} files in directory {path}.");
    }

    public void IndexSyncedConfigDirectory(SyncedConfigDirectory directory,
        bool rehashIfModifiedDataHasntChanged = false)
    {
        if (directory.LocalDirectory == null) return;
        string? rootDirectory = directory.GetRootDirectoryForIndexingOperations();
        if (rootDirectory == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"The directory {directory} does not have a root directory set. Skipping indexing.");
            Console.ResetColor();
            return;
        }
        IndexDirectory(rootDirectory, rehashIfModifiedDataHasntChanged);
    }

    public void UpdateFile(string path)
    {
        DbFile? indexedFile = IndexFile(path);
        if(indexedFile == null) return;
        using (Database db = new Database())
        {
            
            int deletedDuplicates = db.Files.Where(x => !x.IsSynced && x.FullPath == path).ExecuteDelete();
            if (deletedDuplicates > 0)
            {
                Console.WriteLine($"Deleted {deletedDuplicates} duplicate entries from the database.");
            }
            db.Files.Add(indexedFile);
            db.SaveChanges();
        }
    }

    public void StartFilesystemWatcherOnDirectory(string directory)
    {
        FileSystemWatcher watcher = new FileSystemWatcher(directory);
        watcher.Filter = "*";
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;
        watcher.NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.LastWrite;
        watcher.Changed += (obj, args) => UpdateFile(args.FullPath);
        //watcher.Created += (obj, args) => UpdateFile(args.FullPath);
        watcher.Renamed += (obj, args) => UpdateFile(args.FullPath);
        watcher.Deleted += (obj, args) => UpdateFile(args.FullPath);
        Console.WriteLine($"Started watched on {directory}");
        FileWatchers.Add(watcher);
    }

    /// <summary>
    /// Starts filesystem watchers on all directories in the config and updates the index when a file is changed :3
    /// </summary>
    /// <param name="config"></param>
    public void StartFilesystemWatcher(SyncedConfig config)
    {
        foreach (SyncedConfigDirectory dir in config.GetSourceDirectories())
        {
            string? path = dir.GetRootDirectoryForIndexingOperations();
            if (path == null) continue;
            StartFilesystemWatcherOnDirectory(path);
        }
        foreach (SyncedConfigDirectory dir in config.GetTargetDirectories())
        {
            string? path = dir.GetRootDirectoryForIndexingOperations();
            if (path == null) continue;
            StartFilesystemWatcherOnDirectory(path);
        }
    }

    /// <summary>
    /// Does a full scan of all directories container in the synced config therefore returning all files that have been found with their respective indexed state.
    /// </summary>
    /// <param name="syncedConfig"></param>
    /// <param name="rehashIfModifiedDataHasntChanged"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public void IndexConfig(SyncedConfig syncedConfig, bool rehashIfModifiedDataHasntChanged = false)
    {
        List<SyncedConfigDirectory> directories = syncedConfig.GetSourceDirectories();
        directories.AddRange(syncedConfig.GetTargetDirectories());
        foreach (SyncedConfigDirectory directory in directories)
        {
            if (!directory.HasCorrectFolderMarkers())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"The {directory} has no folder markers.");
                Console.ResetColor();
                continue;
            }
            IndexSyncedConfigDirectory(directory, rehashIfModifiedDataHasntChanged);
        }

        using (Database db = new())
        {
            // Detect orphans
            List<SyncedConfigDirectory> targetDirectories = syncedConfig.GetTargetDirectories();
            foreach (SyncedConfigDirectory syncedConfigDirectory in syncedConfig.GetSourceDirectories())
            {
                if (!syncedConfigDirectory.HasCorrectFolderMarkers()) continue;
                string? sourcePath = syncedConfigDirectory.GetRootDirectoryForSyncingOperations();
                if (sourcePath == null) continue;
                List<SyncedConfigDirectory> syncedConfigDirectories = syncedConfig.GetLinkedDirectories(syncedConfigDirectory);
                foreach (SyncedConfigDirectory targetDirectory in targetDirectories)
                {
                    if (targetDirectory.IsSourceDirectory) continue;
                    if (syncedConfigDirectories.Any(x => x.Id == targetDirectory.Id)) continue;
                    // make sure that it actually checks the correct subdirectory
                    targetDirectory.SetSyncedWithDirectory(syncedConfigDirectory);
                    Console.WriteLine($"Checking candidate {targetDirectory}");
                    // now check folder marker
                    if (!targetDirectory.HasCorrectFolderMarkers()) continue;
                    string? path = targetDirectory.GetRootDirectoryForSyncingOperations();
                    if (path == null) continue;

                    foreach (DbFile dbFile in db.Files.Where(x => x.FullPath.StartsWith(path)))
                    {
                        // mark the files as orphaned
                        DbFile newFile = new DbFile(dbFile);
                        newFile.State = DbFileState.Orphaned;
                        newFile.LastSync = null;
                        newFile.IsSynced = false;
                        db.Files.Add(newFile);
                    }
                }
            }
        }
    }
}