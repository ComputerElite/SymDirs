using Microsoft.EntityFrameworkCore;
using SymDirs.Syncing;

namespace SymDirs.Db;

public class Database : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source=database.db"); // ToDo: make this configurable
    
    public DbSet<DbFile> Files { get; set; }
    public DbSet<DbConfigDirectory> ConfigDirectories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbFile>().HasMany(x => x.SyncedWith).WithMany();
    }
}