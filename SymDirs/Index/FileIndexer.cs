using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SymDirs.Db;
using SymDirs.Syncing;

namespace SymDirs.Index;

public class FileIndexer
{
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
        List<DbFile> files = new List<DbFile>();
        if (!Directory.Exists(path)) return;
        List<string> checkedPaths = new List<string>();
        Console.WriteLine($"Indexing directory {path}...");
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            checkedPaths.Add(filePath);
            DbFile? indexedFile = IndexFile(filePath, rehashIfModifiedDataHasntChanged);
            if (indexedFile != null) files.Add(indexedFile);
        }

        using (Database db = new Database())
        {
            List<string> filePaths = files.Select(x => x.FullPath).ToList();
            int deletedDuplicates = db.Files.Where(x => !x.IsSynced && filePaths.Contains(x.FullPath)).ExecuteDelete();
            if (deletedDuplicates > 0)
            {
                Console.WriteLine($"Deleted {deletedDuplicates} duplicate entries from the database.");
            }
            db.Files.AddRange(files);
            List<DbFile> uncheckedFiles = db.Files.Where(x => x.FullPath.StartsWith(path) && !checkedPaths.Contains(x.FullPath)).ToList();
            Console.WriteLine($"Checking {uncheckedFiles.Count} unchecked files...");
            foreach (DbFile uncheckedFile in uncheckedFiles)
            {
                Console.WriteLine();
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

    /// <summary>
    /// Does a full scan of all directories container in the synced config therefore returning all files that have been found with their respective indexed state.
    /// </summary>
    /// <param name="syncedConfig"></param>
    /// <param name="rehashIfModifiedDataHasntChanged"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public void IndexConfig(SyncedConfig syncedConfig, bool rehashIfModifiedDataHasntChanged = false)
    {
        foreach (SyncedConfigDirectory directory in syncedConfig.GetSourceDirectories())
            IndexSyncedConfigDirectory(directory, rehashIfModifiedDataHasntChanged);
        foreach (SyncedConfigDirectory directory in syncedConfig.GetTargetDirectories())
            IndexSyncedConfigDirectory(directory, rehashIfModifiedDataHasntChanged);
    }
}