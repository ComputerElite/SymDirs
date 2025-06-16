using System.Diagnostics;
using System.Web;
using Microsoft.EntityFrameworkCore;
using SymDirs.Db;

namespace SymDirs.Syncing;

public class SyncController
{
    public void ProcessChanges(SyncedConfig config, bool dryRun = true)
    {
        foreach (SyncedConfigDirectory syncedConfigDirectory in config.GetSourceDirectories())
        {
            List<SyncedConfigDirectory> syncedWith = config.GetLinkedDirectories(syncedConfigDirectory);
            List<SyncOperation> syncOperations =
                _computeSyncOperationsForLinkedDirectories(syncedWith);
            
            if (dryRun)
            {
                foreach (SyncOperation syncOperation in syncOperations)
                {
                    Console.WriteLine(syncOperation.ToString());
                }
                continue;
            }
            
            // Apply operations
            _executeOperations(syncOperations, syncedWith);
        }
    }

    private void _executeOperations(List<SyncOperation> syncOperations, List<SyncedConfigDirectory> syncedWith)
    {
        
        DateTime lastSync = DateTime.Now;
        using (Database db = new())
        {
            List<DbConfigDirectory> syncedWithMapping = new();
            // Make sure syncedWith exist in the database, create them if they don't, update them if they do.
            foreach (SyncedConfigDirectory syncedConfigDirectory in syncedWith)
            {
                DbConfigDirectory? existingDirectory = db.ConfigDirectories
                    .FirstOrDefault(x => x.Id == syncedConfigDirectory.Id);
                if (existingDirectory == null)
                {
                    DbConfigDirectory newDir = new DbConfigDirectory
                    {
                        Id = syncedConfigDirectory.Id,
                    };
                    db.ConfigDirectories.Add(newDir);
                    syncedWithMapping.Add(newDir);
                }
                else
                {
                    syncedWithMapping.Add(db.ConfigDirectories.First(x => x.Id == syncedConfigDirectory.Id));
                }
                
            }
            foreach (SyncOperation syncOperation in syncOperations)
            {
                Console.WriteLine($"Executing {syncOperation}");
                switch (syncOperation.Type)
                {
                    case SyncOperationType.CreateLink:
                        LinkCreator.CreateLink(syncOperation.SourcePath, syncOperation.TargetPath);
                        break;
                    case SyncOperationType.Delete:
                        if (false)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Delete is disabled for now");
                            Console.ResetColor();
                        }
                        else
                        {
                            File.Delete(syncOperation.TargetPath);

                            // we need to delete them or they will be maked as synced even though they don't exist.
                            // Files marked as synced will be checked again and again during full scan indexing
                            db.Files.Where(x =>
                                    x.FullPath == syncOperation.SourcePath || x.FullPath == syncOperation.TargetPath)
                                .ExecuteDelete();
                        }
                        break;
                    case SyncOperationType.Unchanged:
                        break;
                }
                
                // ToDo: Fix 'UNIQUE constraint failed: DbConfigDirectoryDbFile.DbFileId, DbConfigDirectoryDbFile.SyncedWithId'.
                db.Files.Where(x =>
                        x.FullPath == syncOperation.SourcePath || x.FullPath == syncOperation.TargetPath)
                    .ForEachAsync(x =>
                    {
                        x.SyncedWith = syncedWithMapping;
                        x.LastSync = lastSync;
                        x.IsSynced = true;
                    });
            }

            db.SaveChanges();
        }
        
    }

    private List<SyncOperation> _computeSyncOperationsForLinkedDirectories(List<SyncedConfigDirectory> directories)
    {
        Dictionary<string, List<DbFile>> changedFilesByRelativePath = new Dictionary<string, List<DbFile>>();
        // We store this here to avoid multiple calls to GetRootDirectoryForSyncingOperations as it's more expensive than a lookup.
        Dictionary<SyncedConfigDirectory, string> pathByDirectory = new Dictionary<SyncedConfigDirectory, string>();
        
        directories.ForEach(x =>
        {
            string? syncRoot = x.GetRootDirectoryForSyncingOperations();
            if (string.IsNullOrEmpty(syncRoot))
            {
                Console.WriteLine($"Skipping directory {x} because it has no root directory for syncing operations.");
                return;
            }
            pathByDirectory.Add(x, syncRoot);
        });
        Console.WriteLine("\n\nProcessing linked directories for sync operations...");
        
        // First get all changed files per directory and also group them by relative path.
        foreach (var directory in directories)
        {
            Console.WriteLine("Processing directory: " + directory);
            List<DbFile> changedFiles = _getChangedFilesForDirectory(directory, directories);
            if (changedFiles.Count > 0)
            {
                foreach (var file in changedFiles)
                {
                    if (!changedFilesByRelativePath.ContainsKey(file.RelativePathToSyncedDirectory))
                    {
                        changedFilesByRelativePath[file.RelativePathToSyncedDirectory] = new List<DbFile>();
                    }
                    changedFilesByRelativePath[file.RelativePathToSyncedDirectory].Add(file);
                }
            }
        }

        List<SyncOperation> syncOperations = new List<SyncOperation>();
        foreach (KeyValuePair<string, List<DbFile>> pathFileKvp in changedFilesByRelativePath)
        {
            // First we check for a Sync conflict, which is when a file has been changed in multiple directories and they got different hashes.
            // As deleted files will have an empty hash deleting is accounted for by the face that a change and a deletion will result in different hashes.
            bool syncConflict = false;
            Dictionary<byte[], List<DbFile>> changedFilesByHash = new(new ByteArrayComparer());

            foreach (DbFile file in pathFileKvp.Value)
            {
                if (!changedFilesByHash.ContainsKey(file.Hash))
                {
                    changedFilesByHash[file.Hash] = new List<DbFile>();
                }
                changedFilesByHash[file.Hash].Add(file);
            }
            bool skippedFirst = false;
            DbFile? mostRecentFile = null;
            foreach (KeyValuePair<byte[], List<DbFile>> hashFileKvp in changedFilesByHash.OrderByDescending(x => x.Value.Count))
            {
                // Skip the first file group as it's assumed to be the most accurate file because the hash has the most entries.
                // Even if it isn't the correct one this won't be an issue cause it'll result in a sync conflict anyway.
                // 
                // Alternatively we could just go with the most recent ModifyTime as the most accurate file. Isn't needed tho as this
                // should work fine.
                if (!skippedFirst)
                {
                    mostRecentFile = hashFileKvp.Value[0];
                    skippedFirst = true;
                    continue;
                }
                
                // If we get to here we got a sync conflict.
                syncConflict = true;
                hashFileKvp.Value.ForEach(x =>
                {
                    syncOperations.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Conflict,
                        SourcePath = mostRecentFile!.FullPath, // If this ever ends up being null, something is very wrong.
                        TargetPath = x.FullPath,
                        AffectedFiles = [mostRecentFile, x]
                    });
                });
            }

            if (syncConflict)
            {
                // ToDo: Handle sync conflict by creating SyncConflict 
                Console.WriteLine(
                    $"Sync conflict detected for file {pathFileKvp.Key}. It has been changed in multiple directories with different hashes.");
                continue;
            }
            
            List<DbFile> changedFiles = changedFilesByHash.First().Value;
            List<string> filesToBeChanged =
                _getFullPathsOfFilesInSyncedDirectory(mostRecentFile!, changedFiles, pathByDirectory);
            // check for deletion operation now
            if (mostRecentFile!.State == DbFileState.Deleted)
            {
                // Now delete the file in all other directories
                filesToBeChanged.AddRange(changedFiles.Select(x => x.FullPath));
                filesToBeChanged.Add(mostRecentFile.FullPath);
                foreach (string file in filesToBeChanged)
                {
                    syncOperations.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Delete,
                        SourcePath = mostRecentFile.FullPath, // origin of the delete operation
                        TargetPath = file,
                        AffectedFiles = [mostRecentFile, new DbFile(mostRecentFile, file)]
                    });
                }
                continue;
            }
            
            // Now we can safely create sync operations for the files, all files have the same hash and are therefore the same file.
            if (pathFileKvp.Value.Count > 1)
            {
                // We now gotta check the inode
                for (int i = 1; i < changedFiles.Count; i++)
                {
                    if (mostRecentFile!.InodeNumber != changedFiles[i].InodeNumber)
                    {
                        // The Inode is different so the file is a literal copy instead of a hard link, it should be replaced with a hard link
                        syncOperations.Add(new SyncOperation
                        {
                            Type = SyncOperationType.CreateLink,
                            SourcePath = mostRecentFile.FullPath,
                            TargetPath = changedFiles[i].FullPath,
                            AffectedFiles = [mostRecentFile, changedFiles[i]]
                        });
                        continue;
                    }
                    // Remove from changed files as we have now processed the file
                    syncOperations.Add(new SyncOperation
                    {
                        Type = SyncOperationType.Unchanged,
                        SourcePath = mostRecentFile.FullPath,
                        TargetPath = changedFiles[i].FullPath,
                        AffectedFiles = [mostRecentFile, changedFiles[i]]
                    });
                }
            }
            // All duplicates have been addressed now. We just gotta create the new links now.
            foreach (string file in filesToBeChanged)
            {
                syncOperations.Add(new SyncOperation
                {
                    Type = SyncOperationType.CreateLink,
                    SourcePath = mostRecentFile.FullPath,
                    TargetPath = file,
                    AffectedFiles = [mostRecentFile, new DbFile(mostRecentFile, file)]
                });
            }

            if (filesToBeChanged.Count == 0)
            {
                syncOperations.Add(new SyncOperation
                {
                    Type = SyncOperationType.Unchanged,
                    SourcePath = mostRecentFile.FullPath,
                    TargetPath = string.Empty,
                    AffectedFiles = [mostRecentFile]
                });
            }
        }

        return syncOperations;
    }

    private List<string> _getFullPathsOfFilesInSyncedDirectory(DbFile file, List<DbFile> excludedFiles, Dictionary<SyncedConfigDirectory, string> configDirectories)
    {
        List<string> fullPaths = new();
        foreach (var configDirectoryKvp in configDirectories)
        {
            if (file.SyncedDirectory == configDirectoryKvp.Key) continue;

            string fullPath = Path.Combine(configDirectoryKvp.Value, file.RelativePathToSyncedDirectory);
            if (excludedFiles.Any(x => x.FullPath == fullPath)) continue;
            fullPaths.Add(fullPath);
        }
        return fullPaths;
    }
    
    /// <summary>
    /// Gets all changed files for the given directory and puplates the relativePath property of the DbFiles.
    /// </summary>
    /// <param name="directory"></param>
    /// <returns></returns>
    private List<DbFile> _getChangedFilesForDirectory(SyncedConfigDirectory directory, List<SyncedConfigDirectory> directories)
    {
        string? rootPath = directory.GetRootDirectoryForSyncingOperations();
        if (string.IsNullOrEmpty(rootPath))
        {
            return new List<DbFile>();
        }
        Uri rootPathUri = new Uri(rootPath);
        List<DbFile> files = new List<DbFile>();
        List<string> neededDirectoryIds = directories.Select(x => x.Id).ToList();
        using (var db = new Database())
        {
            files = db.Files
                .Where(x => (!x.IsSynced || neededDirectoryIds.All(y => x.SyncedWith.Any(x => x.Id == y))) && x.FullPath.StartsWith(rootPath)).ToList();
        }
        files.ForEach(x =>
        {
            x.SetRelativePathToSyncedDirectory(rootPathUri, directory);
        });

        return files;
    }
}