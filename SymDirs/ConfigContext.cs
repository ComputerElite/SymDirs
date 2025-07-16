using Microsoft.Extensions.Logging;
using SymDirs.Syncing;

namespace SymDirs;

public class ConfigContext
{
    public SyncedConfig SyncedConfig;
    public LocalConfig LocalConfig;
    public Config Config;
    public Action<ConfigContext>? OnConfigsUpdated;
    private FileSystemWatcher _syncedWatcher;
    private FileSystemWatcher _localWatcher;
    private ILogger _logger;

    public ConfigContext(string? mainConfigPath)
    {
        Config = Config.Load(mainConfigPath);
        LocalConfig = LocalConfig.Load(Config.LocalConfigPath);
        SyncedConfig = SyncedConfig.Load(Config.SyncedConfigPath, LocalConfig);
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger("ConfigContext");
    }

    private readonly TimeSpan _coalesceWindow = TimeSpan.FromMilliseconds(100);
    private bool _waiting = false;
    private readonly object _lock = new object();
    private void _queueEvent()
    {
        lock (_lock)
        {
            if (_waiting)
            {
                _logger.LogDebug("Dropping update event");
                // We’re already waiting to see if another event comes in
                return;
            }

            _waiting = true;

            // Start a delay to allow other events to arrive
            _ = Task.Run(async () =>
            {
                _logger.LogDebug("Waiting for other config changes (if any)");
                await Task.Delay(_coalesceWindow);
                lock (_lock)
                {
                    _waiting = false;
                    _logger.LogDebug("Informing upstream of config changes");
                    OnConfigsUpdated?.Invoke(this);
                }
            });
        }
    }

    public void StartFilesystemWatchers()
    {
        _logger.LogInformation($"Starting filesystem watcher on {Config.LocalConfigPath}");
        _localWatcher = new FileSystemWatcher(new FileInfo(Config.LocalConfigPath).DirectoryName ?? "");
        _localWatcher.Filter = Path.GetFileName(Config.LocalConfigPath);
        _localWatcher.EnableRaisingEvents = true;
        _localWatcher.Changed += (sender, args) =>
        {
            _logger.LogInformation($"{Config.LocalConfigPath} updated");
            LocalConfig = LocalConfig.Load(Config.LocalConfigPath);
            SyncedConfig.UpdateLocalConfig(LocalConfig);
            _queueEvent();
        };       
        _logger.LogInformation($"Starting filesystem watcher on {Config.SyncedConfigPath}");
        _syncedWatcher = new FileSystemWatcher(new FileInfo(Config.SyncedConfigPath).DirectoryName ?? "");
        _syncedWatcher.Filter = Path.GetFileName(Config.SyncedConfigPath);
        _syncedWatcher.EnableRaisingEvents = true;
        _syncedWatcher.Changed += (sender, args) =>
        {
            _logger.LogInformation($"{Config.SyncedConfigPath} updated");
            SyncedConfig = SyncedConfig.Load(Config.SyncedConfigPath, LocalConfig);
            _queueEvent();
        };
    }
}