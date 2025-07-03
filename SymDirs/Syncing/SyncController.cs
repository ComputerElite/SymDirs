using System.Diagnostics;
using System.Web;
using Microsoft.EntityFrameworkCore;
using SymDirs.Db;

namespace SymDirs.Syncing;

public class SyncController
{
    // ToDo for entire SyncController: Add folder support
    // It should sync folders just like it does files.
    public void ProcessChanges(SyncedConfig config, bool dryRun = true)
    {
        foreach (SyncedConfigDirectory syncedConfigDirectory in config.GetSourceDirectories())
        {
            List<SyncedConfigDirectory> syncedWith = config.GetLinkedDirectories(syncedConfigDirectory);
            List<SyncOperation> syncOperations =
                _computeSyncOperationsForLinkedDirectories(syncedWith);
            syncOperations.AddRange(_fixOrphanedTargetDirectories(config));
            
            if (dryRun)
            {
                foreach (SyncOperation syncOperation in syncOperations)
                {
                    syncOperation.PrintToConsole();
                }
                continue;
            }
            
            // Apply operations
            _executeOperations(syncOperations, syncedWith);
        }
    }

    /// <summary>
    /// Checks whether any target directory has a directory which isn't linked anymore but still exists in it
    /// </summary>
    /// <param name="config"></param>
    /// <param name="dryRun"></param>
    private List<SyncOperation> _fixOrphanedTargetDirectories(SyncedConfig config)
    {
        List<SyncedConfigDirectory> targetDirectories = config.GetTargetDirectories();
        List<SyncOperation> syncOperations = new();
        foreach (SyncedConfigDirectory syncedConfigDirectory in config.GetSourceDirectories())
        {
            if (!syncedConfigDirectory.HasCorrectFolderMarkers()) continue;
            string? sourcePath = syncedConfigDirectory.GetRootDirectoryForSyncingOperations();
            if (sourcePath == null) continue;
            List<SyncedConfigDirectory> syncedConfigDirectories = config.GetLinkedDirectories(syncedConfigDirectory);
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
                Uri rootPathUri = new Uri(path);
                Dictionary<SyncedConfigDirectory, string> rootDirs = new Dictionary<SyncedConfigDirectory, string>();
                foreach (SyncedConfigDirectory sDir in syncedConfigDirectories)
                {
                    string? rDir = sDir.GetRootDirectoryForSyncingOperations();
                    if (rDir == null) continue;
                    rootDirs.Add(sDir, rDir);
                }
                   
                using (Database db = new())
                {
                    List<DbFile> files = db.Files.Where(x => x.IsSynced && x.FullPath.StartsWith(path)).ToList();
                    foreach (DbFile foundFile in files)
                    {
                        syncOperations.Add(new SyncOperation
                        {
                            SourcePath = foundFile.FullPath,
                            TargetPath = foundFile.FullPath,
                            AffectedFiles = [foundFile],
                            Type = SyncOperationType.Delete
                        });
                        foundFile.SetRelativePathToSyncedDirectory(rootPathUri, targetDirectory);
                        db.Files.Remove(foundFile);
                        foreach (var otherDir in rootDirs)
                        {
                            string targetPath = Path.Combine(otherDir.Value, foundFile.RelativePathToSyncedDirectory);
                            syncOperations.Add(new SyncOperation
                            {
                                SourcePath = foundFile.FullPath,
                                TargetPath = targetPath,
                                AffectedFiles = [new DbFile{FullPath = targetPath}],
                                Type = SyncOperationType.UpdateSyncedDirectories
                            });
                        }
                    }
                }
            }
        }

        return syncOperations;
    }

    private void _executeOperations(List<SyncOperation> syncOperations, List<SyncedConfigDirectory> syncedWith)
    {
        // ToDo: Differentiate between files and directories
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

                        }
                        break;
                    case SyncOperationType.RemoveFromIndex:
                        foreach (var syncOperationAffectedFile in syncOperation.AffectedFiles)
                        {
                            db.Files.Remove(syncOperationAffectedFile);
                        }
                        continue;
                    case SyncOperationType.Conflict:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Conflict is disabled for now at {syncOperation.SourcePath} to {syncOperation.TargetPath}");
                        Console.ResetColor();
                        break;  
                    case SyncOperationType.UpdateIndex:
                        break;
                    case SyncOperationType.UpdateSyncedDirectories:
                        for (int i = 0; i < syncOperation.AffectedFiles.Count; i++)
                        {
                            // fetch it from the db and populate entries
                            DbFile? inDbFile = db.Files.FirstOrDefault(x => x.FullPath == syncOperation.AffectedFiles[i].FullPath && x.IsSynced);
                            if (inDbFile == null)
                            {
                                syncOperation.AffectedFiles.RemoveAt(i);
                                i--;
                                continue;
                            }

                            syncOperation.AffectedFiles[i].PopulateFrom(inDbFile);
                        }

                        break;
                }

                foreach (DbFile syncOperationAffectedFile in syncOperation.AffectedFiles)
                {
                    syncOperationAffectedFile.SyncedWith = syncedWithMapping;
                    syncOperationAffectedFile.LastSync = lastSync;
                    syncOperationAffectedFile.IsSynced = true;
                    db.Files.Where(x =>
                            x.FullPath == syncOperationAffectedFile.FullPath)
                        .ExecuteDelete();
                    // we need to delete them or they will be marked as synced even though they don't exist.
                    // Files marked as synced will be checked again and again during full scan indexing
                    if(syncOperation.Type != SyncOperationType.Delete) db.Files.Add(syncOperationAffectedFile);
                }
            }

            db.SaveChanges();
        }
        
    }

    private List<SyncOperation> _computeSyncOperationsForLinkedDirectories(List<SyncedConfigDirectory> directories)
    {
        Dictionary<string, List<DbFile>> changedFilesByRelativePath = new Dictionary<string, List<DbFile>>();
        // We store this here to avoid multiple calls to GetRootDirectoryForSyncingOperations as it's more expensive than a lookup.
        Dictionary<SyncedConfigDirectory, string> pathByDirectory = new Dictionary<SyncedConfigDirectory, string>();
        directories = directories.OrderByDescending(x => x.IsSourceDirectory).ToList();
        
        // Check directories for folder markers
        for (int i = 0; i < directories.Count; i++)
        {
            if (directories[i].HasCorrectFolderMarkers()) continue;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{directories[i]} has incorrect or missing folder markers, skipping sync operations in it");
            Console.ResetColor();
            directories.RemoveAt(i);
            i--;
        }
        
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
                        Type = SyncOperationType.UpdateIndex,
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
                    Type = SyncOperationType.UpdateIndex,
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
                .Where(x => (!x.IsSynced || x.IsSynced && !neededDirectoryIds.All(y => x.SyncedWith.Any(x => x.Id == y))) && x.FullPath.StartsWith(rootPath)).ToList();
        }
        files.ForEach(x =>
        {
            x.SetRelativePathToSyncedDirectory(rootPathUri, directory);
        });

        return files;
    }

    public void RestoreFolderMarkerAssistant(SyncedConfig syncedConfig)
    {
        List<SyncedConfigDirectory> targetDirectories = syncedConfig.GetTargetDirectories();
        foreach (var sourceDirectory in syncedConfig.GetSourceDirectories())
        {
            string? sourcePath = sourceDirectory.GetRootDirectoryForSyncingOperations();
            if(sourcePath == null) continue;
            if (!sourceDirectory.HasCorrectFolderMarkers())
            {
                Console.WriteLine("Do you want to create the folder marker of following directory (y/N)?");
                Console.WriteLine(sourceDirectory.ToString());
                bool createMarker = Console.ReadLine()?.ToLower().Trim() == "y";
                if (!createMarker) continue;
                FolderMarker.CreateDirectoryMarker(sourcePath, sourceDirectory.Id);
            }
            
            foreach (SyncedConfigDirectory targetDirectory in syncedConfig.GetLinkedDirectories(sourceDirectory))
            {
                if (targetDirectory.IsSourceDirectory) continue;
                if (targetDirectory.HasCorrectFolderMarkers()) continue;
                string? targetPath = targetDirectory.GetRootDirectoryForSyncingOperations();
                if (targetPath == null) continue;
                Console.WriteLine("Do you want to create the folder marker of following directory (y/N)?");
                Console.WriteLine(targetDirectory.ToString());
                bool createMarker = Console.ReadLine()?.ToLower().Trim() == "y";
                if (!createMarker) continue;
                string? expectedId = targetDirectory.GetExpectedFolderMarkerForSyncingOperations();
                if (expectedId == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Couldn't get expected folder marker id. This is not supposed to happen. Skipping creation");
                    Console.ResetColor();
                    continue;
                }
                FolderMarker.CreateDirectoryMarker(targetPath, expectedId);
            }
        }
    }
}