using SymDirs;

class Program
{
    static void Main(string[] args)
    {
        MainWindow w = new MainWindow();

        string arg = String.Join(' ', args);

        do
        {
            w.Show(arg);
        } while (arg == "");
    }
}