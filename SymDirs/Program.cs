using System.CommandLine;
using SymDirs;
using SymDirs.Syncing;

class Program
{
    static int Main(string[] args)
    {
        Option<string> configOption = new (
            "--config"
        )
        {
            Description = "path to a config for SymDirs"
        };

        var rootCommand = new RootCommand
        {
            configOption
        };

        Command updateMountsCommand = new Command("update-mounts",
            description: "Updates all bind mounts based on the provided config.");
        rootCommand.Add(updateMountsCommand);
        Option<bool> watchOption = new("--watch")
            { Description = "If provided mounts will be updated when the the config files change" };
        updateMountsCommand.Add(watchOption);
        updateMountsCommand.Add(configOption);

        Command installServiceCommand = new Command("install-service") {Description = "Creates and enables a systemd service for updating the bind mounts based on the provided config. Updates the service if it already exists."};
        rootCommand.Add(installServiceCommand);
        installServiceCommand.Add(configOption);
        
        rootCommand.Description = "Sym Dirs";

        rootCommand.SetAction(parsedResult =>
        {
            BindMountsWindow w = new(new ConfigContext(parsedResult.GetValue(configOption)));

            while(true)
            {
                w.Show();
            };
            return 0;
        });
        updateMountsCommand.SetAction(parsedResult =>
        {
            ConfigContext configs = new (parsedResult.GetValue(configOption));
            bool watch = parsedResult.GetValue(watchOption);
            MountManager.Update(configs, watch);
        });
        installServiceCommand.SetAction(parsedResult =>
        {
            ServiceInstaller.InstallService(parsedResult.GetValue(configOption));
        });
        return rootCommand.Parse(args).Invoke();
    }
}