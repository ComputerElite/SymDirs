namespace SymDirs;

public class InstructionHelper
{
    public static void PrintInstructions(Dictionary<string, string> availableActions)
    {
        
        string availableActionsString = "";
        foreach (KeyValuePair<string,string> keyValuePair in availableActions)
        {
            availableActionsString += $"[{keyValuePair.Key.Substring(0, 1)}] {keyValuePair.Value}{(keyValuePair.Key.StartsWith("4") ? "\n" : "  ")}";
        }
        Console.WriteLine(availableActionsString);
    }
}