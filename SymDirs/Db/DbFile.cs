using System.ComponentModel.DataAnnotations.Schema;
using System.Web;
using Microsoft.EntityFrameworkCore;
using SymDirs.Index;
using SymDirs.Syncing;

namespace SymDirs.Db;

[Index(nameof(LastSync))]
[Index(nameof(LastSync), nameof(FullPath))]
[Index(nameof(FullPath))]
public class DbFile
{
    public DbFile() {}

    /// <summary>
    /// Creates a DbFile, calling this constructor will populate lastModified
    /// </summary>
    /// <param name="path"></param>
    public DbFile(string path)
    {
        FullPath = path;
        Hash = [];
        State = DbFileState.Unknown;
    }

    public DbFile(DbFile copy, string newPath)
    {
        FullPath = newPath;
        Hash = copy.Hash;
        ByteSize = copy.ByteSize;
        InodeNumber = copy.InodeNumber;
        LastModified = copy.LastModified;
        LastSync = copy.LastSync;
        State = copy.State;
        IsSynced = copy.IsSynced;
        RelativePathToSyncedDirectory = copy.RelativePathToSyncedDirectory;
    }
    
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    
    public string FullPath { get; set; } = "";
    public byte[] Hash { get; set; } = [];
    public long ByteSize { get; set; } = 0;
    public ulong? InodeNumber { get; set; } = null;
    public DateTime LastModified { get; set; } = DateTime.MinValue;
    public DateTime? LastSync { get; set; } = null;
    public DbFileState State { get; set; } = DbFileState.Unknown;
    public bool IsSynced { get; set; } = false;
    public List<DbConfigDirectory> SyncedWith { get; set; }

    [NotMapped]
    public string RelativePathToSyncedDirectory = "";
    [NotMapped]
    public SyncedConfigDirectory SyncedDirectory = new SyncedConfigDirectory();

    public string RelativeToString()
    {
        return $"{FullPath} -> {RelativePathToSyncedDirectory}";
    }

    public void SetRelativePathToSyncedDirectory(Uri rootPathUri, SyncedConfigDirectory syncedDirectory)
    {
        RelativePathToSyncedDirectory = HttpUtility.UrlDecode(rootPathUri.MakeRelativeUri(new Uri(FullPath)).ToString());
        SyncedDirectory = syncedDirectory;
    }

    /// <summary>
    /// Populates the inode number of the file. By making a call to the filesystem.
    /// </summary>
    /// <returns></returns>
    public DbFile? PopulateInode()
    {
        if (string.IsNullOrEmpty(FullPath) || !File.Exists(FullPath))
        {
            return null;
        }
        try
        {
            InodeNumber = InodeReader.GetInodeNumber(FullPath);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while getting inode number for {FullPath}: {e.Message}");
            InodeNumber = null;
        }
        return this;
    }
}

public enum DbFileState
{
    Unknown,
    /// <summary>
    /// Files is unchanged, no difference to the tracked version.
    /// </summary>
    Unchanged,
    /// <summary>
    /// File has been modified, content different from the tracked version.
    /// </summary>
    Modified,
    /// <summary>
    /// File has been deleted, while still tracked in the database it doesn't exist anymore.
    /// </summary>
    Deleted,
    /// <summary>
    /// File is new, not tracked before.
    /// </summary>
    New,
    /// <summary>
    /// All changes which have been synced have this state.
    /// </summary>
    Synced
}