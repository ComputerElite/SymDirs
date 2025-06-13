using SymDirs.ReturnTypes;

namespace SymDirs.Syncing;

public class FolderMarker
{
    public const string MarkerFileName = ".symdirs";

    public static string? GetIdOfDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        string markerFilePath = Path.Combine(path, MarkerFileName);
        if (!File.Exists(markerFilePath))
            return null;
        try
        {
            return File.ReadAllText(markerFilePath).Trim();
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error reading marker file at {markerFilePath}: {e.Message}");
            Console.ResetColor();
            return null;
        }
    }

    public static BooleanMessage CreateDirectoryMarker(string path, string directoryId)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directoryId))
        {
            return new BooleanMessage("Path or directory ID cannot be null or empty.", false);
        }

        string markerFilePath = Path.Combine(path, MarkerFileName);
        try
        {
            File.WriteAllText(markerFilePath, directoryId);
            return new BooleanMessage($"Marker file created at {markerFilePath} with ID {directoryId}.", true);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error creating marker file at {markerFilePath}: {e.Message}");
            Console.ResetColor();
            return new BooleanMessage($"Failed to create marker file: {e.Message}", false);
        }
    }
}