using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SymDirs.Db;

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
    
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string Id { get; set; }
    
    public string FullPath { get; set; } = "";
    public byte[] Hash { get; set; } = [];
    public DateTime LastModified { get; set; } = DateTime.MinValue;
    public DateTime? LastSync { get; set; } = null;
    public DbFileState State { get; set; } = DbFileState.Unknown;
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