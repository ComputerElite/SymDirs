using SymDirs.Db;

namespace SymDirs.Syncing;

public class SyncOperation
{
    public string SourcePath { get; set; }
    public string TargetPath { get; set; }
    public SyncOperationType Type { get; set; }
    public List<DbFile> AffectedFiles = new ();

    public override string ToString()
    {
        return $"{Type} {SourcePath} -> {TargetPath}";
    }

    public void PrintToConsole()
    {
        switch (Type)
        {
            case SyncOperationType.CreateLink:
                Console.ForegroundColor = ConsoleColor.Green;
                break;
            case SyncOperationType.Conflict:
                Console.ForegroundColor = ConsoleColor.Yellow;
                break;
            case SyncOperationType.Delete:
                Console.ForegroundColor = ConsoleColor.Red;
                break;
            case SyncOperationType.UpdateIndex:
                Console.ForegroundColor = ConsoleColor.Gray; break;
        }
        Console.WriteLine(ToString());
        Console.ResetColor();
    }
}

public enum SyncOperationType
{
    CreateLink,
    Delete,
    Conflict,
    UpdateIndex
}