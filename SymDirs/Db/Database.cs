using Microsoft.EntityFrameworkCore;

namespace SymDirs.Db;

public class Database : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source=database.db"); // ToDo: make this configurable
    
    public DbSet<DbFile> ChangedFiles { get; set; }
    public DbSet<DbFile> SyncedFiles { get; set; }
    
}