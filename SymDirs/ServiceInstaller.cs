using Microsoft.Extensions.Logging;
using SymDirs.Syncing;

namespace SymDirs;

public class ServiceInstaller
{
    private const string serviceName = "symdirs";
    public static void InstallService(string? configPath)
    {
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        ILogger logger = factory.CreateLogger($"ServiceInstaller");
        string execPath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", "");
        string serviceFilePath = $"/etc/systemd/system/{serviceName}.service";
        string serviceContent = $@"
[Unit]
Description=SymDirs bind mount update daemon{(configPath != null ? $" ({configPath})" : "")}
After=local-fs.target

[Service]
ExecStart={execPath} update-mounts --watch{(configPath != null ? $" --config \"{configPath}\"" : "")}
Restart=always
User=root

[Install]
WantedBy=multi-user.target
";
        logger.LogInformation($"Creating service file at {serviceFilePath}");
        try
        {
            File.WriteAllText(serviceFilePath, serviceContent);
        }
        catch (Exception e)
        {
            logger.LogError($"Couldn't create service file at {serviceFilePath}. Possibly lacking permissions:\n{e}");
            return;
        }

        logger.LogInformation($"Enabling service {serviceName}");
        try
        {
            new MountManager().InvokeProcess("systemctl", "enable symdirs.service");
        }
        catch (Exception e)
        {
            logger.LogError($"Couldn't enable service {serviceName}");
        }
    }
}