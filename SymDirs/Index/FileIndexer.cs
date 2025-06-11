using System.Security.Cryptography;
using SymDirs.Db;

namespace SymDirs.Index;

public class FileIndexer
{
    public byte[] HashFile(string path)
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
    public DbFile? IndexFile(string path, bool rehashIfModifiedDataHasntChanged = false)
    {
        using (var db = new Database())
        {
            // 1. Retrieve old file state from the database
            DbFile? oldEntry = db.SyncedFiles.FirstOrDefault(x => x.FullPath == path);
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
                return null;
            }
            file.LastModified = File.GetLastWriteTime(path);
            
            // Only hash the file if it's been modified according to the modified date
            if(file.LastModified != oldEntry?.LastModified || rehashIfModifiedDataHasntChanged) file.Hash = HashFile(path);
            else file.Hash = oldEntry.Hash;
            
            // The file hasn't been tracked before; therefore, it's new
            if (oldEntry == null)
            {
                file.State = DbFileState.New;
                return file;
            }
            
            // If it was tracked before we can just copy the LastSync of the old entry
            file.LastSync = oldEntry.LastSync;
            
            // Lastly, if the content changed, we mark it as modified
            if (oldEntry.Hash != file.Hash)
            {
                file.State = DbFileState.Modified;
                return file;
            }
            file.State = DbFileState.Unchanged;
            return file;
        }
    }
    
    public List<DbFile> IndexDirectory(string path, bool rehashIfModifiedDataHasntChanged = false)
    {
        List<DbFile> files = new List<DbFile>();
        if (!Directory.Exists(path)) return files;
        
        foreach (string filePath in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
        {
            DbFile? indexedFile = IndexFile(filePath, rehashIfModifiedDataHasntChanged);
            if (indexedFile != null) files.Add(indexedFile);
        }
        
        return files;
    }
}